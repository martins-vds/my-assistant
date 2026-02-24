using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Returns all non-completed tasks with status, time spent today, and priority.
/// When there are more than 20 tasks, groups by status and provides a summary.
/// </summary>
public sealed class GetOpenTasksUseCase
{
    private readonly TaskTrackingService _tracking;

    private const int LargeTaskListThreshold = 20;

    public GetOpenTasksUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public Task<GetOpenTasksResult> ExecuteAsync(CancellationToken ct = default)
    {
        var currentTask = _tracking.GetCurrentTask();
        var openTasks = _tracking.GetOpenTasks();

        var taskSummaries = openTasks.Select(t => new TaskSummary
        {
            Name = t.Name,
            Status = t.Status.ToString(),
            TimeSpentToday = t.GetTimeSpentToday(),
            TotalTimeSpent = t.GetTotalTimeSpent(),
            PriorityRanking = t.PriorityRanking,
            IsCurrent = currentTask is not null && t.Id == currentTask.Id
        }).ToList();

        // For large task lists, provide a grouped summary
        string? groupedSummary = null;
        if (taskSummaries.Count > LargeTaskListThreshold)
        {
            var inProgressCount = taskSummaries.Count(t => t.IsCurrent);
            var pausedCount = taskSummaries.Count - inProgressCount;
            var withPriority = taskSummaries.Where(t => t.PriorityRanking.HasValue).OrderBy(t => t.PriorityRanking).ToList();
            var totalTimeToday = TimeSpan.FromTicks(taskSummaries.Sum(t => t.TimeSpentToday.Ticks));

            groupedSummary = $"You have {taskSummaries.Count} open tasks ({inProgressCount} in progress, {pausedCount} paused). " +
                            $"Total time today: {totalTimeToday:h\\:mm}.";

            if (withPriority.Count > 0)
            {
                var topPriorities = string.Join(", ", withPriority.Take(5).Select(t => $"{t.Name} [P{t.PriorityRanking}]"));
                groupedSummary += $" Top priorities: {topPriorities}.";
            }
        }

        return Task.FromResult(new GetOpenTasksResult
        {
            IsSuccess = true,
            Tasks = taskSummaries,
            CurrentTaskName = currentTask?.Name,
            GroupedSummary = groupedSummary
        });
    }
}

public sealed record GetOpenTasksResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<TaskSummary> Tasks { get; init; } = Array.Empty<TaskSummary>();
    public string? CurrentTaskName { get; init; }
    /// <summary>
    /// Non-null when there are more than 20 tasks, providing a concise grouped summary.
    /// </summary>
    public string? GroupedSummary { get; init; }
}

public sealed record TaskSummary
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public TimeSpan TimeSpentToday { get; init; }
    public TimeSpan TotalTimeSpent { get; init; }
    public int? PriorityRanking { get; init; }
    public bool IsCurrent { get; init; }
}
