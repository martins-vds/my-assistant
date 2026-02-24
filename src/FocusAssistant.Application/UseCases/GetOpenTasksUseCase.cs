using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Returns all non-completed tasks with status, time spent today, and priority.
/// </summary>
public sealed class GetOpenTasksUseCase
{
    private readonly TaskTrackingService _tracking;

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

        return Task.FromResult(new GetOpenTasksResult
        {
            IsSuccess = true,
            Tasks = taskSummaries,
            CurrentTaskName = currentTask?.Name
        });
    }
}

public sealed record GetOpenTasksResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<TaskSummary> Tasks { get; init; } = Array.Empty<TaskSummary>();
    public string? CurrentTaskName { get; init; }
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
