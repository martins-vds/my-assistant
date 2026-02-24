namespace FocusAssistant.Domain.ValueObjects;

/// <summary>
/// Represents a time interval during which a user worked on a task.
/// </summary>
public sealed record TimeLogEntry
{
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }

    public TimeSpan Duration => EndTime.HasValue
        ? EndTime.Value - StartTime
        : DateTime.UtcNow - StartTime;

    public bool IsActive => !EndTime.HasValue;

    public TimeLogEntry(DateTime startTime, DateTime? endTime = null)
    {
        if (endTime.HasValue && endTime.Value < startTime)
            throw new ArgumentException("End time cannot be before start time.", nameof(endTime));

        StartTime = startTime;
        EndTime = endTime;
    }

    public TimeLogEntry Stop(DateTime endTime)
    {
        if (endTime < StartTime)
            throw new ArgumentException("End time cannot be before start time.", nameof(endTime));

        return this with { EndTime = endTime };
    }
}
