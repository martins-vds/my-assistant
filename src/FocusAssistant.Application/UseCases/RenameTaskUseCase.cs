using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Renames an existing task.
/// </summary>
public sealed class RenameTaskUseCase
{
    private readonly TaskTrackingService _tracking;

    public RenameTaskUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public async Task<RenameTaskResult> ExecuteAsync(string oldName, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return RenameTaskResult.Error("Both old and new task names are required.");

        if (_tracking.HasTaskWithName(newName))
            return RenameTaskResult.Error($"A task named '{newName}' already exists.");

        try
        {
            var task = _tracking.RenameTask(oldName, newName);
            await _tracking.SaveAsync(ct);
            return RenameTaskResult.Success(oldName, task.Name);
        }
        catch (InvalidOperationException ex)
        {
            return RenameTaskResult.Error(ex.Message);
        }
    }
}

public sealed record RenameTaskResult
{
    public bool IsSuccess { get; init; }
    public string? OldName { get; init; }
    public string? NewName { get; init; }
    public string? ErrorMessage { get; init; }

    public static RenameTaskResult Success(string oldName, string newName) => new()
    {
        IsSuccess = true,
        OldName = oldName,
        NewName = newName
    };

    public static RenameTaskResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
