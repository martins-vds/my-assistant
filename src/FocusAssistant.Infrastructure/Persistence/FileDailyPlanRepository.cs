using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Infrastructure.Persistence;

public sealed class FileDailyPlanRepository : IDailyPlanRepository
{
    private readonly JsonFileStore<DailyPlan> _store;

    public FileDailyPlanRepository()
    {
        _store = new JsonFileStore<DailyPlan>(DataDirectory.GetFilePath("daily-plans.json"));
    }

    public FileDailyPlanRepository(string filePath)
    {
        _store = new JsonFileStore<DailyPlan>(filePath);
    }

    public async Task<DailyPlan?> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var plans = await _store.ReadAllAsync(ct);
        return plans.FirstOrDefault(p => p.DateFor == date);
    }

    public async Task<DailyPlan?> GetLatestAsync(CancellationToken ct = default)
    {
        var plans = await _store.ReadAllAsync(ct);
        return plans.OrderByDescending(p => p.DateFor).FirstOrDefault();
    }

    public async Task<IReadOnlyList<DailyPlan>> GetAllAsync(CancellationToken ct = default)
    {
        var plans = await _store.ReadAllAsync(ct);
        return plans.OrderByDescending(p => p.DateFor).ToList().AsReadOnly();
    }

    public async Task SaveAsync(DailyPlan plan, CancellationToken ct = default)
    {
        var plans = await _store.ReadAllAsync(ct);
        var index = plans.FindIndex(p => p.Id == plan.Id);
        if (index >= 0)
            plans[index] = plan;
        else
            plans.Add(plan);

        await _store.WriteAllAsync(plans, ct);
    }
}
