using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Application.Services;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

/// <summary>
/// Tracks idle time and paused-task durations.
/// Determines when reminders should fire.
/// Supports escalating suppression: tasks that have been reminded about
/// but not acted on are suppressed until the next session.
/// </summary>
public sealed class ReminderScheduler
{
    private readonly TaskTrackingService _tracking;
    private readonly IUserPreferencesRepository _preferencesRepository;
    private DateTime _lastInteractionTime;
    private bool _isFocusSuppressed;

    /// <summary>
    /// Tracks task IDs that have been reminded once but not acted on.
    /// After one reminder, the task is suppressed for the rest of the session.
    /// </summary>
    private readonly HashSet<Guid> _suppressedTaskIds = new();

    /// <summary>
    /// Tracks task IDs that have been reminded at least once in this session.
    /// Used to implement escalating suppression.
    /// </summary>
    private readonly HashSet<Guid> _remindedTaskIds = new();

    public ReminderScheduler(TaskTrackingService tracking, IUserPreferencesRepository preferencesRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _lastInteractionTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a user interaction, resetting the idle timer.
    /// If the user switched to a task that was previously reminded, clear its suppression.
    /// </summary>
    public void RecordInteraction()
    {
        _lastInteractionTime = DateTime.UtcNow;
        _isFocusSuppressed = false;

        // If the user acted on a task (e.g., switched to it), clear suppression for current task
        var currentTask = _tracking.GetCurrentTask();
        if (currentTask is not null)
        {
            _suppressedTaskIds.Remove(currentTask.Id);
            _remindedTaskIds.Remove(currentTask.Id);
        }
    }

    /// <summary>
    /// Mark a task reminder as acknowledged. After one reminder without action,
    /// the task is suppressed for the rest of this session.
    /// </summary>
    public void AcknowledgeReminder(Guid taskId)
    {
        if (_remindedTaskIds.Contains(taskId))
        {
            // Second reminder — suppress for the rest of the session
            _suppressedTaskIds.Add(taskId);
        }
        else
        {
            // First reminder — track it
            _remindedTaskIds.Add(taskId);
        }
    }

    /// <summary>
    /// Reset all suppression state (e.g., on new session start).
    /// </summary>
    public void ResetSuppression()
    {
        _suppressedTaskIds.Clear();
        _remindedTaskIds.Clear();
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
    /// Returns true if the user has been idle longer than their configured threshold.
    /// Triggers when there is no active task, OR when the user has a task in progress
    /// but has been idle for 3x the threshold (likely stepped away).
    /// </summary>
    public async Task<bool> IsIdleCheckInDueAsync(CancellationToken ct = default)
    {
        if (_isFocusSuppressed)
            return false;

        var prefs = await _preferencesRepository.GetAsync(ct);
        var threshold = prefs?.IdleCheckInThreshold ?? TimeSpan.FromMinutes(5);

        var idleTime = GetIdleTime();
        var currentTask = _tracking.GetCurrentTask();

        // No active task — check at normal threshold
        if (currentTask is null)
            return idleTime >= threshold;

        // Active task but user idle for 3x threshold — they likely stepped away
        return idleTime >= threshold * 3;
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
            // Skip tasks that have been escalation-suppressed
            if (_suppressedTaskIds.Contains(task.Id))
                continue;

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

        var end = lastLog.EndTime ?? DateTime.UtcNow;
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
