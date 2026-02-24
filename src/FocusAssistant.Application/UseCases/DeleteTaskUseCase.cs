using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Deletes a task. Requires confirmation to prevent accidental deletion.
/// </summary>
public sealed class DeleteTaskUseCase
{
    private readonly TaskTrackingService _tracking;

    public DeleteTaskUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public async Task<DeleteTaskResult> ExecuteAsync(string name, bool confirmed = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DeleteTaskResult.Error("Task name is required.");

        if (!_tracking.HasTaskWithName(name))
            return DeleteTaskResult.Error($"No task named '{name}' found.");

        if (!confirmed)
            return DeleteTaskResult.NeedsConfirmation(name);

        try
        {
            _tracking.DeleteTask(name);
            await _tracking.SaveAsync(ct);
            return DeleteTaskResult.Success(name);
        }
        catch (InvalidOperationException ex)
        {
            return DeleteTaskResult.Error(ex.Message);
        }
    }
}

public sealed record DeleteTaskResult
{
    public bool IsSuccess { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string? TaskName { get; init; }
    public string? ErrorMessage { get; init; }

    public static DeleteTaskResult Success(string name) => new()
    {
        IsSuccess = true,
        TaskName = name
    };

    public static DeleteTaskResult NeedsConfirmation(string name) => new()
    {
        RequiresConfirmation = true,
        TaskName = name
    };

    public static DeleteTaskResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
