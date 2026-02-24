using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Saves the user's priority ranking for the next day as a DailyPlan entity.
/// </summary>
public sealed class SetPrioritiesUseCase
{
    private readonly TaskTrackingService _tracking;
    private readonly IDailyPlanRepository _dailyPlanRepository;

    public SetPrioritiesUseCase(TaskTrackingService tracking, IDailyPlanRepository dailyPlanRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _dailyPlanRepository = dailyPlanRepository ?? throw new ArgumentNullException(nameof(dailyPlanRepository));
    }

    /// <summary>
    /// Sets priority order for given task names. Creates a DailyPlan for tomorrow.
    /// </summary>
    public async Task<SetPrioritiesResult> ExecuteAsync(IReadOnlyList<string> orderedTaskNames, string? note = null, CancellationToken ct = default)
    {
        if (orderedTaskNames is null || orderedTaskNames.Count == 0)
            return SetPrioritiesResult.Error("No tasks specified for prioritization.");

        // Validate entries: no empty/whitespace names
        if (orderedTaskNames.Any(string.IsNullOrWhiteSpace))
            return SetPrioritiesResult.Error("Task names cannot be empty.");

        // Check for duplicate names
        var distinct = orderedTaskNames.Select(n => n.Trim().ToLowerInvariant()).ToHashSet();
        if (distinct.Count != orderedTaskNames.Count)
            return SetPrioritiesResult.Error("Duplicate task names are not allowed in priority list.");

        var taskIds = new List<Guid>();
        foreach (var name in orderedTaskNames)
        {
            var task = _tracking.FindTaskByName(name);
            if (task is null)
                return SetPrioritiesResult.Error($"Task '{name}' not found.");

            task.SetPriority(taskIds.Count + 1);
            taskIds.Add(task.Id);
        }

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var existingPlan = await _dailyPlanRepository.GetByDateAsync(tomorrow, ct);

        var plan = existingPlan ?? new DailyPlan(tomorrow);
        plan.SetTaskPriorities(taskIds);

        if (note is not null)
            plan.AddNote(note);

        await _dailyPlanRepository.SaveAsync(plan, ct);
        await _tracking.SaveAsync(ct);

        return SetPrioritiesResult.Success(
            orderedTaskNames,
            tomorrow);
    }
}

public sealed record SetPrioritiesResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<string> OrderedTaskNames { get; init; } = Array.Empty<string>();
    public DateOnly? PlanDate { get; init; }
    public string? ErrorMessage { get; init; }

    public static SetPrioritiesResult Success(IReadOnlyList<string> orderedNames, DateOnly planDate) => new()
    {
        IsSuccess = true,
        OrderedTaskNames = orderedNames,
        PlanDate = planDate
    };

    public static SetPrioritiesResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
