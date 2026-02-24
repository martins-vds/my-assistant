using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Infrastructure.Persistence;

public sealed class FileNoteRepository : INoteRepository
{
    private readonly JsonFileStore<TaskNote> _store;

    public FileNoteRepository()
    {
        _store = new JsonFileStore<TaskNote>(DataDirectory.GetFilePath("notes.json"));
    }

    public FileNoteRepository(string filePath)
    {
        _store = new JsonFileStore<TaskNote>(filePath);
    }

    public async Task<TaskNote?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var notes = await _store.ReadAllAsync(ct);
        return notes.FirstOrDefault(n => n.Id == id);
    }

    public async Task<IReadOnlyList<TaskNote>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        var notes = await _store.ReadAllAsync(ct);
        return notes.Where(n => n.TaskId == taskId).OrderBy(n => n.CreatedAt).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<TaskNote>> GetStandaloneNotesAsync(CancellationToken ct = default)
    {
        var notes = await _store.ReadAllAsync(ct);
        return notes.Where(n => n.IsStandalone).OrderBy(n => n.CreatedAt).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<TaskNote>> GetAllAsync(CancellationToken ct = default)
    {
        var notes = await _store.ReadAllAsync(ct);
        return notes.OrderBy(n => n.CreatedAt).ToList().AsReadOnly();
    }

    public async Task SaveAsync(TaskNote note, CancellationToken ct = default)
    {
        var notes = await _store.ReadAllAsync(ct);
        var index = notes.FindIndex(n => n.Id == note.Id);
        if (index >= 0)
            notes[index] = note;
        else
            notes.Add(note);

        await _store.WriteAllAsync(notes, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var notes = await _store.ReadAllAsync(ct);
        notes.RemoveAll(n => n.Id == id);
        await _store.WriteAllAsync(notes, ct);
    }
}
