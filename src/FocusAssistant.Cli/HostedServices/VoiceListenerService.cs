using FocusAssistant.Application.Interfaces;
using FocusAssistant.Application.Services;
using FocusAssistant.Cli.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Cli.HostedServices;

/// <summary>
/// Background service that loops: wait for wake word → capture speech →
/// send to CopilotAgentSession → speak response.
/// In stub mode (stdin/stdout), the loop is: read line → send → print.
/// Tracks last interaction timestamp and reports to ReminderScheduler.
/// </summary>
public sealed class VoiceListenerService : BackgroundService
{
    private readonly IVoiceInputService _input;
    private readonly IVoiceOutputService _output;
    private readonly CopilotAgentSession _agentSession;
    private readonly ReminderScheduler _reminderScheduler;
    private readonly ILogger<VoiceListenerService> _logger;

    public VoiceListenerService(
        IVoiceInputService input,
        IVoiceOutputService output,
        CopilotAgentSession agentSession,
        ReminderScheduler reminderScheduler,
        ILogger<VoiceListenerService> logger)
    {
        _input = input;
        _output = output;
        _agentSession = agentSession;
        _reminderScheduler = reminderScheduler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Voice listener starting...");

        try
        {
            await _agentSession.InitializeAsync();
            _logger.LogInformation("Agent session initialized. Ready for input.");
            await _output.SpeakAsync("Focus Assistant ready. How can I help?", stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize agent session");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for wake word (in stub mode, this returns immediately)
                var wakeDetected = await _input.WaitForWakeWordAsync(stoppingToken);
                if (!wakeDetected)
                    continue;

                // Capture speech
                var userInput = await _input.ListenAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(userInput))
                    continue;

                if (IsExitCommand(userInput))
                {
                    await _output.SpeakAsync("Goodbye! Great work today.", stoppingToken);
                    _logger.LogInformation("User requested exit");
                    break;
                }

                _logger.LogDebug("User said: {Input}", userInput);

                // Send to Copilot and get response
                var response = await _agentSession.SendCommandAsync(userInput);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    await _output.SpeakAsync(response, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing voice input");
                await _output.SpeakAsync("Sorry, something went wrong. Please try again.", stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Voice listener stopping...");
        await _agentSession.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    private static bool IsExitCommand(string input) =>
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("bye", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("goodbye", StringComparison.OrdinalIgnoreCase);
}
