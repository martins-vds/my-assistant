using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Marks a task as completed. Stops time tracking.
/// Returns a list of paused tasks as suggestions for what to work on next.
/// </summary>
public sealed class CompleteTaskUseCase
{
    private readonly TaskTrackingService _tracking;

    public CompleteTaskUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public async Task<CompleteTaskResult> ExecuteAsync(string? name = null, CancellationToken ct = default)
    {
        // Normalize whitespace-only name to null (complete current task)
        if (string.IsNullOrWhiteSpace(name))
            name = null;

        try
        {
            var task = _tracking.CompleteTask(name);
            await _tracking.SaveAsync(ct);

            var pausedTasks = _tracking.GetPausedTasks()
                .Select(t => t.Name)
                .ToList();

            return CompleteTaskResult.Success(task.Name, pausedTasks);
        }
        catch (InvalidOperationException ex)
        {
            return CompleteTaskResult.Error(ex.Message);
        }
    }
}

public sealed record CompleteTaskResult
{
    public bool IsSuccess { get; init; }
    public string? TaskName { get; init; }
    public IReadOnlyList<string> PausedTaskSuggestions { get; init; } = Array.Empty<string>();
    public string? ErrorMessage { get; init; }

    public static CompleteTaskResult Success(string taskName, IReadOnlyList<string> pausedSuggestions) => new()
    {
        IsSuccess = true,
        TaskName = taskName,
        PausedTaskSuggestions = pausedSuggestions
    };

    public static CompleteTaskResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
