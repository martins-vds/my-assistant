using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Repositories;

public interface IDailyPlanRepository
{
    Task<DailyPlan?> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<DailyPlan?> GetLatestAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DailyPlan>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(DailyPlan plan, CancellationToken ct = default);
}
