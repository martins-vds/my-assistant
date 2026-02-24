using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Repositories;

public interface ISessionRepository
{
    Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkSession?> GetLatestAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkSession>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(WorkSession session, CancellationToken ct = default);
}
