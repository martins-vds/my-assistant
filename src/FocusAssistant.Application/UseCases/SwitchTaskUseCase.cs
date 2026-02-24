using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Switches to an existing or new task. Auto-pauses the current task.
/// Reads back the most recent note when resuming a previously paused task.
/// </summary>
public sealed class SwitchTaskUseCase
{
    private readonly TaskTrackingService _tracking;
    private readonly INoteRepository _noteRepository;

    public SwitchTaskUseCase(TaskTrackingService tracking, INoteRepository noteRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
    }

    public async Task<SwitchTaskResult> ExecuteAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return SwitchTaskResult.Error("Task name cannot be empty.");

        try
        {
            var previousTask = _tracking.GetCurrentTask();
            var task = _tracking.SwitchTask(name);
            await _tracking.SaveAsync(ct);

            // Check if the task has notes to read back
            string? lastNote = null;
            if (task.NoteIds.Count > 0)
            {
                var notes = await _noteRepository.GetByTaskIdAsync(task.Id, ct);
                lastNote = notes.LastOrDefault()?.Content;
            }

            return SwitchTaskResult.Success(
                task.Name,
                previousTask?.Name,
                wasCreated: previousTask is not null && _tracking.FindTaskByName(name) is null || task.CreatedAt > DateTime.UtcNow.AddSeconds(-2),
                lastNote);
        }
        catch (InvalidOperationException ex)
        {
            return SwitchTaskResult.Error(ex.Message);
        }
    }
}

public sealed record SwitchTaskResult
{
    public bool IsSuccess { get; init; }
    public string? TaskName { get; init; }
    public string? PreviousTaskName { get; init; }
    public bool WasCreated { get; init; }
    public string? LastNote { get; init; }
    public string? ErrorMessage { get; init; }

    public static SwitchTaskResult Success(string taskName, string? previousTaskName, bool wasCreated, string? lastNote) => new()
    {
        IsSuccess = true,
        TaskName = taskName,
        PreviousTaskName = previousTaskName,
        WasCreated = wasCreated,
        LastNote = lastNote
    };

    public static SwitchTaskResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
