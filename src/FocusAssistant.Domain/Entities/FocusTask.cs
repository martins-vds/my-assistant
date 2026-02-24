using System.Text.Json.Serialization;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Domain.Entities;

using TaskStatus = ValueObjects.TaskStatus;

/// <summary>
/// Represents a unit of work the user is tracking.
/// </summary>
public sealed class FocusTask
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public TaskStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int? PriorityRanking { get; private set; }
    public ReminderInterval? ReminderInterval { get; private set; }
    public List<TimeLogEntry> TimeLogs { get; private set; } = new();
    public List<Guid> NoteIds { get; private set; } = new();

    [JsonConstructor]
    private FocusTask() { Name = string.Empty; } // For deserialization

    public FocusTask(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Task name cannot be empty.", nameof(name));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Status = TaskStatus.InProgress;
        CreatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        if (Status == TaskStatus.Completed || Status == TaskStatus.Archived)
            throw new InvalidOperationException($"Cannot start a {Status} task.");

        Status = TaskStatus.InProgress;
        TimeLogs.Add(new TimeLogEntry(DateTime.UtcNow));
    }

    public void Pause()
    {
        if (Status != TaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot pause a task that is {Status}.");

        Status = TaskStatus.Paused;
        StopActiveTimeLog();
    }

    public void Complete()
    {
        if (Status == TaskStatus.Completed || Status == TaskStatus.Archived)
            throw new InvalidOperationException($"Task is already {Status}.");

        Status = TaskStatus.Completed;
        StopActiveTimeLog();
    }

    public void Archive()
    {
        if (Status == TaskStatus.Archived)
            throw new InvalidOperationException("Task is already archived.");

        Status = TaskStatus.Archived;
        StopActiveTimeLog();
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Task name cannot be empty.", nameof(newName));

        Name = newName.Trim();
    }

    public void SetPriority(int ranking)
    {
        if (ranking < 1)
            throw new ArgumentException("Priority ranking must be positive.", nameof(ranking));

        PriorityRanking = ranking;
    }

    public void SetReminderInterval(ReminderInterval interval)
    {
        ReminderInterval = interval ?? throw new ArgumentNullException(nameof(interval));
    }

    public void ClearReminderInterval()
    {
        ReminderInterval = null;
    }

    public void AddNoteId(Guid noteId)
    {
        NoteIds.Add(noteId);
    }

    public void MergeFrom(FocusTask other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (other.Id == Id) throw new InvalidOperationException("Cannot merge a task with itself.");

        foreach (var noteId in other.NoteIds)
            NoteIds.Add(noteId);

        foreach (var log in other.TimeLogs)
            TimeLogs.Add(log);
    }

    public TimeSpan GetTimeSpentToday()
    {
        var today = DateTime.UtcNow.Date;
        return TimeLogs
            .Where(t => t.StartTime.Date == today)
            .Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration);
    }

    public TimeSpan GetTotalTimeSpent()
    {
        return TimeLogs.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration);
    }

    private void StopActiveTimeLog()
    {
        var activeIndex = TimeLogs.FindLastIndex(t => t.IsActive);
        if (activeIndex >= 0)
        {
            TimeLogs[activeIndex] = TimeLogs[activeIndex].Stop(DateTime.UtcNow);
        }
    }
}
