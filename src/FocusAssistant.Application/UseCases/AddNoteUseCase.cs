using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Attaches a timestamped note to the current or specified task.
/// If no task is active and no task name is given, stores as a standalone note.
/// </summary>
public sealed class AddNoteUseCase
{
    private readonly TaskTrackingService _tracking;
    private readonly INoteRepository _noteRepository;

    public AddNoteUseCase(TaskTrackingService tracking, INoteRepository noteRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
    }

    public async Task<AddNoteResult> ExecuteAsync(string content, string? taskName = null, bool storeAsStandalone = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return AddNoteResult.Error("Note content cannot be empty.");

        FocusTask? task = null;

        if (taskName is not null)
        {
            task = _tracking.FindTaskByName(taskName);
            if (task is null)
                return AddNoteResult.Error($"Task '{taskName}' not found.");
        }
        else
        {
            task = _tracking.GetCurrentTask();
        }

        // No task found and not explicitly standalone
        if (task is null && !storeAsStandalone)
        {
            return AddNoteResult.NeedsTaskSelection(content);
        }

        var note = new TaskNote(content, task?.Id);
        await _noteRepository.SaveAsync(note, ct);

        if (task is not null)
        {
            task.AddNoteId(note.Id);
            await _tracking.SaveAsync(ct);
        }

        return task is not null
            ? AddNoteResult.Success(note.Id, content, task.Name)
            : AddNoteResult.Success(note.Id, content, null);
    }
}

public sealed record AddNoteResult
{
    public bool IsSuccess { get; init; }
    public bool RequiresTaskSelection { get; init; }
    public Guid? NoteId { get; init; }
    public string? Content { get; init; }
    public string? TaskName { get; init; }
    public string? ErrorMessage { get; init; }

    public static AddNoteResult Success(Guid noteId, string content, string? taskName) => new()
    {
        IsSuccess = true,
        NoteId = noteId,
        Content = content,
        TaskName = taskName
    };

    public static AddNoteResult NeedsTaskSelection(string content) => new()
    {
        RequiresTaskSelection = true,
        Content = content
    };

    public static AddNoteResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
