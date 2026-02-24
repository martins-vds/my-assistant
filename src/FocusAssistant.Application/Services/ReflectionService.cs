using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.Services;

/// <summary>
/// Generates daily summaries for end-of-day reflection:
/// completed tasks, open tasks, and time per task.
/// </summary>
public sealed class ReflectionService
{
    private readonly TaskTrackingService _tracking;
    private readonly INoteRepository _noteRepository;

    public ReflectionService(TaskTrackingService tracking, INoteRepository noteRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
    }

    /// <summary>
    /// Generates a daily summary covering tasks worked on, time spent, and any standalone notes.
    /// </summary>
    public async Task<DailySummary> GenerateDailySummaryAsync(CancellationToken ct = default)
    {
        var completedTasks = _tracking.GetCompletedTasks()
            .Where(t => t.CreatedAt.Date == DateTime.UtcNow.Date ||
                        t.TimeLogs.Any(tl => tl.StartTime.Date == DateTime.UtcNow.Date))
            .Select(t => new TaskSummaryItem(t.Name, t.GetTimeSpentToday(), true))
            .ToList();

        var openTasks = _tracking.GetOpenTasks()
            .Select(t => new TaskSummaryItem(t.Name, t.GetTimeSpentToday(), false))
            .ToList();

        var totalTimeToday = completedTasks.Concat(openTasks)
            .Aggregate(TimeSpan.Zero, (sum, t) => sum + t.TimeSpentToday);

        var standaloneNotes = await _noteRepository.GetStandaloneNotesAsync(ct);
        var todayStandalone = standaloneNotes
            .Where(n => n.CreatedAt.Date == DateTime.UtcNow.Date)
            .Select(n => n.Content)
            .ToList();

        return new DailySummary(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            CompletedTasks: completedTasks,
            OpenTasks: openTasks,
            TotalTimeToday: totalTimeToday,
            StandaloneNotes: todayStandalone);
    }
}

/// <summary>
/// A single task's summary for the daily reflection.
/// </summary>
public sealed record TaskSummaryItem(string Name, TimeSpan TimeSpentToday, bool IsCompleted = false);

/// <summary>
/// The complete daily summary used for end-of-day reflection.
/// </summary>
public sealed record DailySummary(
    DateOnly Date,
    IReadOnlyList<TaskSummaryItem> CompletedTasks,
    IReadOnlyList<TaskSummaryItem> OpenTasks,
    TimeSpan TotalTimeToday,
    IReadOnlyList<string> StandaloneNotes);
