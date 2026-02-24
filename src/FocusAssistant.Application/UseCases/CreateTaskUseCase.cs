using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Creates a new task. Auto-pauses any in-progress task.
/// Checks for duplicate names and returns a disambiguation prompt if needed.
/// </summary>
public sealed class CreateTaskUseCase
{
    private readonly TaskTrackingService _tracking;

    public CreateTaskUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public async Task<CreateTaskResult> ExecuteAsync(string name, bool force = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CreateTaskResult.Error("Task name cannot be empty.");

        // Check for duplicate names
        if (!force && _tracking.HasTaskWithName(name))
        {
            var existing = _tracking.FindTaskByName(name)!;
            return CreateTaskResult.Duplicate(existing.Name, existing.Status.ToString());
        }

        var task = _tracking.CreateTask(name);
        await _tracking.SaveAsync(ct);

        var currentBefore = _tracking.GetPausedTasks();
        string? pausedTaskName = null;
        if (currentBefore.Count > 0)
        {
            // The most recently paused task (which was just auto-paused)
            var justPaused = currentBefore.FirstOrDefault(t => t.Id != task.Id);
            pausedTaskName = justPaused?.Name;
        }

        return CreateTaskResult.Success(task.Name, pausedTaskName);
    }
}

public sealed record CreateTaskResult
{
    public bool IsSuccess { get; init; }
    public bool IsDuplicate { get; init; }
    public string? TaskName { get; init; }
    public string? PausedTaskName { get; init; }
    public string? ExistingStatus { get; init; }
    public string? ErrorMessage { get; init; }

    public static CreateTaskResult Success(string taskName, string? pausedTaskName) => new()
    {
        IsSuccess = true,
        TaskName = taskName,
        PausedTaskName = pausedTaskName
    };

    public static CreateTaskResult Duplicate(string existingName, string status) => new()
    {
        IsDuplicate = true,
        TaskName = existingName,
        ExistingStatus = status
    };

    public static CreateTaskResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
