using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Merges a source task into a target task, combining notes and time logs.
/// The source task is deleted after merge.
/// </summary>
public sealed class MergeTasksUseCase
{
    private readonly TaskTrackingService _tracking;

    public MergeTasksUseCase(TaskTrackingService tracking)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public async Task<MergeTasksResult> ExecuteAsync(string sourceName, string targetName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName))
            return MergeTasksResult.Error("Both source and target task names are required.");

        if (sourceName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            return MergeTasksResult.Error("Cannot merge a task with itself.");

        try
        {
            var merged = _tracking.MergeTasks(sourceName, targetName);
            await _tracking.SaveAsync(ct);
            return MergeTasksResult.Success(sourceName, merged.Name);
        }
        catch (InvalidOperationException ex)
        {
            return MergeTasksResult.Error(ex.Message);
        }
    }
}

public sealed record MergeTasksResult
{
    public bool IsSuccess { get; init; }
    public string? SourceName { get; init; }
    public string? TargetName { get; init; }
    public string? ErrorMessage { get; init; }

    public static MergeTasksResult Success(string sourceName, string targetName) => new()
    {
        IsSuccess = true,
        SourceName = sourceName,
        TargetName = targetName
    };

    public static MergeTasksResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
