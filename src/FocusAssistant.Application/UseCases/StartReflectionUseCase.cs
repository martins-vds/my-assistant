using FocusAssistant.Application.Services;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Triggers a structured end-of-day reflection that summarizes the day's work.
/// </summary>
public sealed class StartReflectionUseCase
{
    private readonly ReflectionService _reflectionService;
    private readonly TaskTrackingService _tracking;

    public StartReflectionUseCase(ReflectionService reflectionService, TaskTrackingService tracking)
    {
        _reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public async Task<ReflectionResult> ExecuteAsync(CancellationToken ct = default)
    {
        var summary = await _reflectionService.GenerateDailySummaryAsync(ct);

        var lines = new List<string>
        {
            $"📋 Daily Summary for {summary.Date:yyyy-MM-dd}",
            ""
        };

        if (summary.CompletedTasks.Count > 0)
        {
            lines.Add("✅ Completed:");
            foreach (var task in summary.CompletedTasks)
            {
                var time = task.TimeSpentToday > TimeSpan.Zero
                    ? $" ({task.TimeSpentToday:h\\:mm})"
                    : "";
                lines.Add($"  • {task.Name}{time}");
            }
            lines.Add("");
        }

        if (summary.OpenTasks.Count > 0)
        {
            lines.Add("⏳ Still Open:");
            foreach (var task in summary.OpenTasks)
            {
                var time = task.TimeSpentToday > TimeSpan.Zero
                    ? $" ({task.TimeSpentToday:h\\:mm})"
                    : "";
                lines.Add($"  • {task.Name}{time}");
            }
            lines.Add("");
        }

        if (summary.TotalTimeToday > TimeSpan.Zero)
        {
            lines.Add($"⏱️ Total time tracked today: {summary.TotalTimeToday:h\\:mm}");
            lines.Add("");
        }

        if (summary.StandaloneNotes.Count > 0)
        {
            lines.Add("📝 Standalone notes from today:");
            foreach (var note in summary.StandaloneNotes)
            {
                lines.Add($"  • {note}");
            }
            lines.Add("");
        }

        if (summary.OpenTasks.Count > 0)
        {
            lines.Add("Would you like to set priorities for tomorrow?");
        }
        else
        {
            lines.Add("No open tasks — you're all caught up! 🎉");
        }

        return new ReflectionResult
        {
            IsSuccess = true,
            Summary = string.Join("\n", lines),
            HasOpenTasks = summary.OpenTasks.Count > 0,
            OpenTaskNames = summary.OpenTasks.Select(t => t.Name).ToList()
        };
    }
}

public sealed record ReflectionResult
{
    public bool IsSuccess { get; init; }
    public string? Summary { get; init; }
    public bool HasOpenTasks { get; init; }
    public IReadOnlyList<string> OpenTaskNames { get; init; } = Array.Empty<string>();
    public string? ErrorMessage { get; init; }
}
