using FocusAssistant.Application.Interfaces;
using FocusAssistant.Application.Services;
using FocusAssistant.Cli.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Cli.HostedServices;

/// <summary>
/// Background service that periodically checks for idle check-ins and paused-task
/// reminders, then sends proactive prompts via the CopilotAgentSession.
/// </summary>
public sealed class ReminderBackgroundService : BackgroundService
{
    private readonly ReminderScheduler _scheduler;
    private readonly IVoiceOutputService _output;
    private readonly CopilotAgentSession _agentSession;
    private readonly ILogger<ReminderBackgroundService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    public ReminderBackgroundService(
        ReminderScheduler scheduler,
        IVoiceOutputService output,
        CopilotAgentSession agentSession,
        ILogger<ReminderBackgroundService> logger)
    {
        _scheduler = scheduler;
        _output = output;
        _agentSession = agentSession;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder background service starting...");

        // Give the main voice listener time to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking reminders");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken ct)
    {
        // Check idle check-in
        if (await _scheduler.IsIdleCheckInDueAsync(ct))
        {
            _logger.LogDebug("Idle check-in triggered");
            var response = await _agentSession.SendCommandAsync(
                "[SYSTEM] The user has been idle for a while and has no active task. " +
                "Gently ask what they're working on or suggest tasks they could resume.");

            if (!string.IsNullOrWhiteSpace(response))
            {
                await _output.SpeakAsync(response, ct);
                _scheduler.RecordInteraction(); // Prevent repeated idle check-ins
            }
        }

        // Check paused-task reminders
        var dueReminders = await _scheduler.GetDueRemindersAsync(ct);
        if (dueReminders.Count > 0)
        {
            var taskNames = string.Join(", ", dueReminders.Select(r => $"'{r.TaskName}' (paused {r.PausedDuration.TotalMinutes:F0}m)"));
            _logger.LogDebug("Paused task reminders due: {Tasks}", taskNames);

            var response = await _agentSession.SendCommandAsync(
                $"[SYSTEM] Reminder: These paused tasks haven't been touched in a while: {taskNames}. " +
                "Briefly mention them and ask if the user wants to switch to one.");

            if (!string.IsNullOrWhiteSpace(response))
            {
                await _output.SpeakAsync(response, ct);
                _scheduler.RecordInteraction();
            }
        }
    }
}
