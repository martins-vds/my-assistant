using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Retrieves all notes for a task in chronological order, or returns standalone notes.
/// </summary>
public sealed class GetTaskNotesUseCase
{
    private readonly TaskTrackingService _tracking;
    private readonly INoteRepository _noteRepository;

    public GetTaskNotesUseCase(TaskTrackingService tracking, INoteRepository noteRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
    }

    public async Task<GetTaskNotesResult> ExecuteAsync(string? taskName = null, CancellationToken ct = default)
    {
        if (taskName is not null)
        {
            var task = _tracking.FindTaskByName(taskName);
            if (task is null)
                return GetTaskNotesResult.Error($"Task '{taskName}' not found.");

            var notes = await _noteRepository.GetByTaskIdAsync(task.Id, ct);
            return GetTaskNotesResult.Success(task.Name, notes);
        }

        // No task name: return notes for the current task, or standalone if none
        var current = _tracking.GetCurrentTask();
        if (current is not null)
        {
            var notes = await _noteRepository.GetByTaskIdAsync(current.Id, ct);
            return GetTaskNotesResult.Success(current.Name, notes);
        }

        // No current task: return standalone notes
        var standalone = await _noteRepository.GetStandaloneNotesAsync(ct);
        return GetTaskNotesResult.Success(null, standalone);
    }
}

public sealed record GetTaskNotesResult
{
    public bool IsSuccess { get; init; }
    public string? TaskName { get; init; }
    public IReadOnlyList<TaskNote> Notes { get; init; } = Array.Empty<TaskNote>();
    public string? ErrorMessage { get; init; }

    public static GetTaskNotesResult Success(string? taskName, IReadOnlyList<TaskNote> notes) => new()
    {
        IsSuccess = true,
        TaskName = taskName,
        Notes = notes
    };

    public static GetTaskNotesResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
