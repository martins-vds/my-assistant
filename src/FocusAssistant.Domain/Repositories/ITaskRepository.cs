using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Repositories;

using TaskStatus = ValueObjects.TaskStatus;

public interface ITaskRepository
{
    Task<FocusTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FocusTask?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FocusTask>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FocusTask>> GetByStatusAsync(TaskStatus status, CancellationToken ct = default);
    Task SaveAsync(FocusTask task, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveAllAsync(IEnumerable<FocusTask> tasks, CancellationToken ct = default);
}
