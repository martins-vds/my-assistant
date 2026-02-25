using FocusAssistant.Application.Interfaces;
using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Cli.Agent;
using FocusAssistant.Domain.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Cli.HostedServices;

/// <summary>
/// Background service that periodically checks for idle check-ins, paused-task
/// reminders, and scheduled reflection time, then sends proactive prompts via the CopilotAgentSession.
/// </summary>
public sealed class ReminderBackgroundService : BackgroundService
{
    private readonly ReminderScheduler _scheduler;
    private readonly IVoiceOutputService _output;
    private readonly CopilotAgentSession _agentSession;
    private readonly IUserPreferencesRepository _prefsRepository;
    private readonly StartReflectionUseCase _startReflection;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private bool _reflectionPromptedToday;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReminderTimeout = TimeSpan.FromSeconds(30);

    public ReminderBackgroundService(
        ReminderScheduler scheduler,
        IVoiceOutputService output,
        CopilotAgentSession agentSession,
        IUserPreferencesRepository prefsRepository,
        StartReflectionUseCase startReflection,
        ILogger<ReminderBackgroundService> logger)
    {
        _scheduler = scheduler;
        _output = output;
        _agentSession = agentSession;
        _prefsRepository = prefsRepository;
        _startReflection = startReflection;
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
            var response = await SendWithTimeoutAsync(
                "[SYSTEM] The user has been idle for a while and has no active task. " +
                "Gently ask what they're working on or suggest tasks they could resume.", ct);

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

            var response = await SendWithTimeoutAsync(
                $"[SYSTEM] Reminder: These paused tasks haven't been touched in a while: {taskNames}. " +
                "Briefly mention them and ask if the user wants to switch to one.", ct);

            if (!string.IsNullOrWhiteSpace(response))
            {
                await _output.SpeakAsync(response, ct);
                // Acknowledge each reminded task for escalating suppression
                foreach (var r in dueReminders)
                    _scheduler.AcknowledgeReminder(r.TaskId);
                _scheduler.RecordInteraction();
            }
        }

        // Check scheduled reflection time
        await CheckReflectionTimeAsync(ct);
    }

    /// <summary>
    /// Sends a command to the agent with a timeout so a hung session
    /// never blocks the entire reminder loop.
    /// </summary>
    private async Task<string> SendWithTimeoutAsync(string prompt, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ReminderTimeout);

        try
        {
            return await _agentSession.SendCommandAsync(prompt, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Reminder SendCommandAsync timed out after {Seconds}s", ReminderTimeout.TotalSeconds);
            return string.Empty;
        }
    }

    private async Task CheckReflectionTimeAsync(CancellationToken ct)
    {
        if (_reflectionPromptedToday)
            return;

        var prefs = await _prefsRepository.GetAsync(ct);
        if (prefs?.AutomaticReflectionTime is null)
            return;

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var reflectionTime = prefs.AutomaticReflectionTime.Value;

        // Check if current time is within the check window of the reflection time
        var minutesSinceReflectionTime = (now.ToTimeSpan() - reflectionTime.ToTimeSpan()).TotalMinutes;
        if (minutesSinceReflectionTime >= 0 && minutesSinceReflectionTime < 2)
        {
            _logger.LogInformation("Scheduled reflection time reached");
            _reflectionPromptedToday = true;

            var reflectionResult = await _startReflection.ExecuteAsync(ct);
            if (reflectionResult.IsSuccess)
            {
                var response = await SendWithTimeoutAsync(
                    $"[SYSTEM] It's reflection time! Here's the daily summary:\n{reflectionResult.Summary}\n" +
                    "Present this to the user and guide them through setting priorities for tomorrow.", ct);

                if (!string.IsNullOrWhiteSpace(response))
                    await _output.SpeakAsync(response, ct);
            }
        }
    }
}
