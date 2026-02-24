using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Repositories;

public interface INoteRepository
{
    Task<TaskNote?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskNote>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskNote>> GetStandaloneNotesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TaskNote>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(TaskNote note, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
