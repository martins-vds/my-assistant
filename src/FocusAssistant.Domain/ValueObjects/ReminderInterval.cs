namespace FocusAssistant.Domain.ValueObjects;

/// <summary>
/// Represents a reminder interval for paused task notifications.
/// </summary>
public sealed record ReminderInterval
{
    public static readonly ReminderInterval Default = new(TimeSpan.FromHours(1));

    public TimeSpan Duration { get; init; }
    public bool IsPerTaskOverride { get; init; }

    public ReminderInterval(TimeSpan duration, bool isPerTaskOverride = false)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentException("Reminder interval must be positive.", nameof(duration));

        Duration = duration;
        IsPerTaskOverride = isPerTaskOverride;
    }

    public static ReminderInterval FromHours(double hours, bool isPerTaskOverride = false)
        => new(TimeSpan.FromHours(hours), isPerTaskOverride);

    public static ReminderInterval FromMinutes(double minutes, bool isPerTaskOverride = false)
        => new(TimeSpan.FromMinutes(minutes), isPerTaskOverride);
}
