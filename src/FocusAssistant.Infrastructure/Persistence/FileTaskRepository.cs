using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Infrastructure.Persistence;

using TaskStatus = Domain.ValueObjects.TaskStatus;

public sealed class FileTaskRepository : ITaskRepository
{
    private readonly JsonFileStore<FocusTask> _store;

    public FileTaskRepository()
    {
        _store = new JsonFileStore<FocusTask>(DataDirectory.GetFilePath("tasks.json"));
    }

    public FileTaskRepository(string filePath)
    {
        _store = new JsonFileStore<FocusTask>(filePath);
    }

    public async Task<FocusTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tasks = await _store.ReadAllAsync(ct);
        return tasks.FirstOrDefault(t => t.Id == id);
    }

    public async Task<FocusTask?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var tasks = await _store.ReadAllAsync(ct);
        return tasks.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            t.Status != TaskStatus.Archived);
    }

    public async Task<IReadOnlyList<FocusTask>> GetAllAsync(CancellationToken ct = default)
    {
        var tasks = await _store.ReadAllAsync(ct);
        return tasks.AsReadOnly();
    }

    public async Task<IReadOnlyList<FocusTask>> GetByStatusAsync(TaskStatus status, CancellationToken ct = default)
    {
        var tasks = await _store.ReadAllAsync(ct);
        return tasks.Where(t => t.Status == status).ToList().AsReadOnly();
    }

    public async Task SaveAsync(FocusTask task, CancellationToken ct = default)
    {
        var tasks = await _store.ReadAllAsync(ct);
        var index = tasks.FindIndex(t => t.Id == task.Id);
        if (index >= 0)
            tasks[index] = task;
        else
            tasks.Add(task);

        await _store.WriteAllAsync(tasks, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tasks = await _store.ReadAllAsync(ct);
        tasks.RemoveAll(t => t.Id == id);
        await _store.WriteAllAsync(tasks, ct);
    }

    public async Task SaveAllAsync(IEnumerable<FocusTask> tasksToSave, CancellationToken ct = default)
    {
        var existingTasks = await _store.ReadAllAsync(ct);
        foreach (var task in tasksToSave)
        {
            var index = existingTasks.FindIndex(t => t.Id == task.Id);
            if (index >= 0)
                existingTasks[index] = task;
            else
                existingTasks.Add(task);
        }
        await _store.WriteAllAsync(existingTasks, ct);
    }
}
