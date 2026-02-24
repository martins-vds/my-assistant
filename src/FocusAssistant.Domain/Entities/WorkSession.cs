using System.Text.Json.Serialization;

namespace FocusAssistant.Domain.Entities;

/// <summary>
/// Represents a continuous period of interaction (typically one workday).
/// Lifecycle is implicit — created on startup, ended on shutdown or reflection.
/// </summary>
public sealed class WorkSession
{
    public Guid Id { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public List<Guid> TaskIdsWorkedOn { get; private set; } = new();
    public string? ReflectionSummary { get; private set; }
    public bool IsActive => !EndTime.HasValue;

    [JsonConstructor]
    private WorkSession() { } // For deserialization

    public WorkSession(DateTime? startTime = null)
    {
        Id = Guid.NewGuid();
        StartTime = startTime ?? DateTime.UtcNow;
    }

    public void RecordTaskWorkedOn(Guid taskId)
    {
        if (!TaskIdsWorkedOn.Contains(taskId))
            TaskIdsWorkedOn.Add(taskId);
    }

    public void End(string? reflectionSummary = null)
    {
        if (EndTime.HasValue)
            throw new InvalidOperationException("Session is already ended.");

        EndTime = DateTime.UtcNow;
        ReflectionSummary = reflectionSummary;
    }

    public void SetReflectionSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Reflection summary cannot be empty.", nameof(summary));

        ReflectionSummary = summary.Trim();
    }
}
