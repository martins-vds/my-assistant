using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Loads previous day's plan, open tasks, task ages, and provides a morning briefing.
/// </summary>
public sealed class GetMorningBriefingUseCase
{
    private readonly TaskTrackingService _tracking;
    private readonly IDailyPlanRepository _planRepository;
    private readonly INoteRepository _noteRepository;

    public GetMorningBriefingUseCase(
        TaskTrackingService tracking,
        IDailyPlanRepository planRepository,
        INoteRepository noteRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
        _noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
    }

    public async Task<MorningBriefingResult> ExecuteAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await _planRepository.GetByDateAsync(today, ct);

        var openTasks = _tracking.GetOpenTasks();
        var lines = new List<string> { $"☀️ Good morning! Here's your briefing for {today:yyyy-MM-dd}:", "" };

        // Show yesterday's plan/priorities if available
        if (plan is not null && plan.OrderedTaskIds.Count > 0)
        {
            lines.Add("📋 Your priorities for today:");
            var rank = 1;
            foreach (var taskId in plan.OrderedTaskIds)
            {
                var task = openTasks.FirstOrDefault(t => t.Id == taskId);
                if (task is not null)
                {
                    var age = (DateTime.UtcNow - task.CreatedAt).Days;
                    var ageText = age > 0 ? $" ({age}d old)" : " (new)";
                    lines.Add($"  {rank}. {task.Name}{ageText}");
                }
                else
                {
                    lines.Add($"  {rank}. (task no longer open)");
                }
                rank++;
            }

            if (plan.Notes.Count > 0)
            {
                lines.Add("");
                lines.Add("📝 Notes from yesterday's planning:");
                foreach (var note in plan.Notes)
                    lines.Add($"  • {note}");
            }
            lines.Add("");
        }

        // Show all open tasks (including any not in the plan)
        var unplannedTasks = plan is not null
            ? openTasks.Where(t => !plan.OrderedTaskIds.Contains(t.Id)).ToList()
            : openTasks.ToList();

        if (plan is null && openTasks.Count > 0)
        {
            lines.Add("📋 Open tasks carried over:");
            foreach (var task in openTasks)
            {
                var age = (DateTime.UtcNow - task.CreatedAt).Days;
                var ageText = age > 0 ? $" ({age}d old)" : " (new)";
                lines.Add($"  • {task.Name}{ageText}");
            }
            lines.Add("");
        }
        else if (unplannedTasks.Count > 0)
        {
            lines.Add("Also open but not in your priority list:");
            foreach (var task in unplannedTasks)
            {
                var age = (DateTime.UtcNow - task.CreatedAt).Days;
                var ageText = age > 0 ? $" ({age}d old)" : " (new)";
                lines.Add($"  • {task.Name}{ageText}");
            }
            lines.Add("");
        }

        // Check for standalone notes
        var standaloneNotes = await _noteRepository.GetStandaloneNotesAsync(ct);
        if (standaloneNotes.Count > 0)
        {
            lines.Add($"📝 You have {standaloneNotes.Count} standalone note(s) to review.");
            lines.Add("");
        }

        if (openTasks.Count == 0)
        {
            lines.Add("No open tasks — you're starting fresh today! What would you like to work on?");
        }
        else
        {
            lines.Add("What would you like to start with?");
        }

        return new MorningBriefingResult
        {
            IsSuccess = true,
            Briefing = string.Join("\n", lines),
            HasPlan = plan is not null,
            OpenTaskCount = openTasks.Count
        };
    }
}

public sealed record MorningBriefingResult
{
    public bool IsSuccess { get; init; }
    public string? Briefing { get; init; }
    public bool HasPlan { get; init; }
    public int OpenTaskCount { get; init; }
    public string? ErrorMessage { get; init; }
}
