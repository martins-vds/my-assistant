using System.Text.Json.Serialization;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Domain.Entities;

/// <summary>
/// User configuration set during onboarding and adjustable at any time.
/// </summary>
public sealed class UserPreferences
{
    public Guid Id { get; private set; }
    public ReminderInterval DefaultReminderInterval { get; private set; }
    public TimeSpan IdleCheckInThreshold { get; private set; }
    public TimeOnly? AutomaticReflectionTime { get; private set; }
    public string WakeWord { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [JsonConstructor]
    private UserPreferences() { WakeWord = "Hey Focus"; DefaultReminderInterval = ReminderInterval.Default; } // For deserialization

    public UserPreferences(
        ReminderInterval? defaultReminderInterval = null,
        TimeSpan? idleCheckInThreshold = null,
        TimeOnly? automaticReflectionTime = null,
        string wakeWord = "Hey Focus")
    {
        if (string.IsNullOrWhiteSpace(wakeWord))
            throw new ArgumentException("Wake word cannot be empty.", nameof(wakeWord));

        Id = Guid.NewGuid();
        DefaultReminderInterval = defaultReminderInterval ?? ReminderInterval.Default;
        IdleCheckInThreshold = idleCheckInThreshold ?? TimeSpan.FromMinutes(5);
        AutomaticReflectionTime = automaticReflectionTime;
        WakeWord = wakeWord.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDefaultReminderInterval(ReminderInterval interval)
    {
        DefaultReminderInterval = interval ?? throw new ArgumentNullException(nameof(interval));
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetIdleCheckInThreshold(TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero)
            throw new ArgumentException("Idle threshold must be positive.", nameof(threshold));

        IdleCheckInThreshold = threshold;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAutomaticReflectionTime(TimeOnly? time)
    {
        AutomaticReflectionTime = time;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetWakeWord(string wakeWord)
    {
        if (string.IsNullOrWhiteSpace(wakeWord))
            throw new ArgumentException("Wake word cannot be empty.", nameof(wakeWord));

        WakeWord = wakeWord.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
