using FocusAssistant.Application.Interfaces;
using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Cli.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Cli.HostedServices;

/// <summary>
/// Background service that loops: wait for wake word → capture speech →
/// send to CopilotAgentSession → speak response.
/// In text mode (stdin/stdout), the loop is: read line → send → print.
/// In voice mode: wake word detection → STT → Copilot → TTS with barge-in.
/// Tracks last interaction timestamp and reports to ReminderScheduler.
/// Triggers morning briefing on new-day session start.
/// Includes graceful shutdown, auto-restart on errors, and long-running stability.
/// </summary>
public sealed class VoiceListenerService : BackgroundService
{
    private readonly IVoiceInputService _input;
    private readonly IVoiceOutputService _output;
    private readonly IWakeWordDetector _wakeWordDetector;
    private readonly CopilotAgentSession _agentSession;
    private readonly ReminderScheduler _reminderScheduler;
    private readonly TaskTrackingService _tracking;
    private readonly GetMorningBriefingUseCase _morningBriefing;
    private readonly ILogger<VoiceListenerService> _logger;
    private readonly bool _useTextMode;

    /// <summary>
    /// Maximum consecutive errors before the service pauses to prevent a tight error loop.
    /// </summary>
    private const int MaxConsecutiveErrors = 5;

    /// <summary>
    /// Delay between retries after consecutive errors.
    /// </summary>
    private static readonly TimeSpan ErrorBackoffDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum delay during exponential backoff for agent re-initialization.
    /// </summary>
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(2);

    public VoiceListenerService(
        IVoiceInputService input,
        IVoiceOutputService output,
        IWakeWordDetector wakeWordDetector,
        CopilotAgentSession agentSession,
        ReminderScheduler reminderScheduler,
        TaskTrackingService tracking,
        GetMorningBriefingUseCase morningBriefing,
        ILogger<VoiceListenerService> logger,
        bool useTextMode = false)
    {
        _input = input;
        _output = output;
        _wakeWordDetector = wakeWordDetector;
        _agentSession = agentSession;
        _reminderScheduler = reminderScheduler;
        _tracking = tracking;
        _morningBriefing = morningBriefing;
        _logger = logger;
        _useTextMode = useTextMode;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Voice listener starting (mode: {Mode})...",
            _useTextMode ? "text" : "voice");

        // Initialize agent session with retry logic
        if (!await InitializeWithRetryAsync(stoppingToken))
            return;

        // Check for morning briefing on new session
        if (_tracking.IsNewSession)
        {
            await SendMorningBriefingAsync(stoppingToken);
        }
        else
        {
            await _output.SpeakAsync("Focus Assistant ready. How can I help?", stoppingToken);
        }

        // Main interaction loop with error recovery
        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for wake word
                bool wakeDetected;
                if (_useTextMode)
                {
                    // In text mode, skip wake word — always ready
                    wakeDetected = true;
                }
                else
                {
                    wakeDetected = await _wakeWordDetector.WaitForWakeWordAsync(stoppingToken);
                }

                if (!wakeDetected)
                    continue;

                // In voice mode, stop any ongoing TTS for barge-in
                if (!_useTextMode)
                {
                    await _output.StopAsync(stoppingToken);
                }

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

                // Record user activity for idle detection
                _reminderScheduler.RecordInteraction();

                // Send to Copilot and get response
                var response = await _agentSession.SendCommandAsync(userInput);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    await _output.SpeakAsync(response, stoppingToken);
                }

                // Reset consecutive error counter on success
                consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Session not initialized"))
            {
                // Agent session lost — attempt re-initialization
                _logger.LogWarning("Agent session lost — attempting re-initialization");
                if (!await InitializeWithRetryAsync(stoppingToken))
                    break;

                consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error processing input (consecutive errors: {Count})", consecutiveErrors);

                if (consecutiveErrors >= MaxConsecutiveErrors)
                {
                    _logger.LogWarning("Too many consecutive errors ({Count}) — pausing for {Delay}s",
                        consecutiveErrors, ErrorBackoffDelay.TotalSeconds);
                    await _output.SpeakAsync(
                        "I'm having some trouble. Give me a moment to reset.", stoppingToken);

                    try
                    {
                        await Task.Delay(ErrorBackoffDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    consecutiveErrors = 0;
                }
                else
                {
                    await _output.SpeakAsync("Sorry, something went wrong. Please try again.", stoppingToken);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Voice listener stopping...");

        // Clean up wake word detector
        if (_wakeWordDetector is IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing wake word detector");
            }
        }

        await _agentSession.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Initializes the agent session with exponential backoff retry logic.
    /// Returns true if initialization succeeded, false if it should give up.
    /// </summary>
    private async Task<bool> InitializeWithRetryAsync(CancellationToken ct)
    {
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                attempt++;
                _logger.LogInformation("Initializing agent session (attempt {Attempt})...", attempt);
                await _agentSession.InitializeAsync(ct);
                _logger.LogInformation("Agent session initialized. Ready for input.");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize agent session (attempt {Attempt})", attempt);

                if (attempt >= 3)
                {
                    _logger.LogCritical("Failed to initialize after {Attempts} attempts. Giving up.", attempt);
                    await _output.SpeakAsync(
                        "I'm unable to connect to the AI service. Please check your setup and try again.", ct);
                    return false;
                }

                _logger.LogWarning("Retrying in {Delay}s...", delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxReconnectDelay.Ticks));
            }
        }

        return false;
    }

    private static bool IsExitCommand(string input) =>
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("bye", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("goodbye", StringComparison.OrdinalIgnoreCase);

    private async Task SendMorningBriefingAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("New session detected — sending morning briefing");
            var briefing = await _morningBriefing.ExecuteAsync(ct);
            if (briefing.IsSuccess && briefing.OpenTaskCount > 0)
            {
                var response = await _agentSession.SendCommandAsync(
                    $"[SYSTEM] This is a new session. Present the morning briefing to the user:\n{briefing.Briefing}");

                if (!string.IsNullOrWhiteSpace(response))
                    await _output.SpeakAsync(response, ct);
            }
            else
            {
                await _output.SpeakAsync("Good morning! Focus Assistant ready. What would you like to work on?", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate morning briefing");
            await _output.SpeakAsync("Focus Assistant ready. How can I help?", ct);
        }
    }
}
