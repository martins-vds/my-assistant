using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

/// <summary>
/// Archives old completed tasks to keep the active task list clean.
/// Supports archiving all completed tasks or only those completed before a given number of days.
/// </summary>
public sealed class ArchiveTasksUseCase
{
    private readonly TaskTrackingService _tracking;

    public ArchiveTasksUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    /// <summary>
    /// Archive all completed tasks, or completed tasks older than the specified number of days.
    /// </summary>
    public async Task<ArchiveTasksResult> ExecuteAsync(int? olderThanDays = null, CancellationToken ct = default)
    {
        try
        {
            var completedTasks = _tracking.GetCompletedTasks();

            if (completedTasks.Count == 0)
                return ArchiveTasksResult.Error("No completed tasks to archive.");

            var tasksToArchive = completedTasks;

            if (olderThanDays.HasValue)
            {
                if (olderThanDays.Value < 0)
                    return ArchiveTasksResult.Error("Days must be a non-negative number.");

                var cutoff = DateTime.UtcNow.AddDays(-olderThanDays.Value);
                tasksToArchive = completedTasks
                    .Where(t =>
                    {
                        var lastLog = t.TimeLogs.LastOrDefault();
                        var completedTime = lastLog?.EndTime ?? t.CreatedAt;
                        return completedTime < cutoff;
                    })
                    .ToList();

                if (tasksToArchive.Count == 0)
                    return ArchiveTasksResult.Error($"No completed tasks older than {olderThanDays.Value} days.");
            }

            foreach (var task in tasksToArchive)
            {
                task.Archive();
            }

            await _tracking.SaveAsync(ct);

            var names = tasksToArchive.Select(t => t.Name).ToList();
            return ArchiveTasksResult.Success(names);
        }
        catch (InvalidOperationException ex)
        {
            return ArchiveTasksResult.Error(ex.Message);
        }
    }
}

public sealed record ArchiveTasksResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<string>? ArchivedTaskNames { get; init; }
    public string? ErrorMessage { get; init; }

    public static ArchiveTasksResult Success(IReadOnlyList<string> names) => new()
    {
        IsSuccess = true,
        ArchivedTaskNames = names
    };

    public static ArchiveTasksResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
