using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Application.Services;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

/// <summary>
/// Tracks idle time and paused-task durations.
/// Determines when reminders should fire.
/// </summary>
public sealed class ReminderScheduler
{
    private readonly TaskTrackingService _tracking;
    private readonly IUserPreferencesRepository _preferencesRepository;
    private DateTime _lastInteractionTime;
    private bool _isFocusSuppressed;

    public ReminderScheduler(TaskTrackingService tracking, IUserPreferencesRepository preferencesRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _lastInteractionTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a user interaction, resetting the idle timer.
    /// </summary>
    public void RecordInteraction()
    {
        _lastInteractionTime = DateTime.UtcNow;
        _isFocusSuppressed = false;
    }

    /// <summary>
    /// Suppress reminders during active focus (user is currently engaged).
    /// </summary>
    public void SuppressDuringFocus()
    {
        _isFocusSuppressed = true;
    }

    /// <summary>
    /// Resume reminder checking after focus suppression.
    /// </summary>
    public void ResumeFocusReminders()
    {
        _isFocusSuppressed = false;
    }

    /// <summary>
    /// Gets the time elapsed since the last user interaction.
    /// </summary>
    public TimeSpan GetIdleTime() => DateTime.UtcNow - _lastInteractionTime;

    /// <summary>
    /// Checks if an idle check-in is due based on user preferences.
    /// Returns true if the user has been idle longer than their configured threshold
    /// and there is no task currently in progress.
    /// </summary>
    public async Task<bool> IsIdleCheckInDueAsync(CancellationToken ct = default)
    {
        if (_isFocusSuppressed)
            return false;

        var prefs = await _preferencesRepository.GetAsync(ct);
        var threshold = prefs?.IdleCheckInThreshold ?? TimeSpan.FromMinutes(15);

        var idleTime = GetIdleTime();
        var currentTask = _tracking.GetCurrentTask();

        // Idle check-in: user has been idle and has no current task
        return currentTask is null && idleTime >= threshold;
    }

    /// <summary>
    /// Gets paused tasks that are due for a reminder based on their configured interval.
    /// </summary>
    public async Task<IReadOnlyList<PausedTaskReminder>> GetDueRemindersAsync(CancellationToken ct = default)
    {
        if (_isFocusSuppressed)
            return Array.Empty<PausedTaskReminder>();

        var prefs = await _preferencesRepository.GetAsync(ct);
        var defaultInterval = prefs?.DefaultReminderInterval ?? ReminderInterval.Default;

        var pausedTasks = _tracking.GetPausedTasks();
        var reminders = new List<PausedTaskReminder>();

        foreach (var task in pausedTasks)
        {
            var interval = task.ReminderInterval ?? defaultInterval;
            var pausedDuration = GetPausedDuration(task);

            if (pausedDuration >= interval.Duration)
            {
                reminders.Add(new PausedTaskReminder(
                    task.Name,
                    task.Id,
                    pausedDuration,
                    interval.Duration));
            }
        }

        return reminders;
    }

    /// <summary>
    /// Gets the duration a task has been paused since its last time log ended.
    /// </summary>
    private static TimeSpan GetPausedDuration(FocusTask task)
    {
        var lastLog = task.TimeLogs.LastOrDefault();
        if (lastLog is null)
            return TimeSpan.Zero;

        // If the time log is still active, the task isn't paused
        if (lastLog.IsActive)
            return TimeSpan.Zero;

        var end = lastLog.End ?? DateTime.UtcNow;
        return DateTime.UtcNow - end;
    }
}

/// <summary>
/// Represents a reminder that a paused task is overdue for attention.
/// </summary>
public sealed record PausedTaskReminder(
    string TaskName,
    Guid TaskId,
    TimeSpan PausedDuration,
    TimeSpan ReminderInterval);
