using System.Text.Json.Serialization;

namespace FocusAssistant.Domain.Entities;

/// <summary>
/// The user's prioritized task list generated during end-of-day reflection.
/// </summary>
public sealed class DailyPlan
{
    public Guid Id { get; private set; }
    public DateOnly DateFor { get; private set; }
    public List<Guid> OrderedTaskIds { get; private set; } = new();
    public List<string> Notes { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }

    [JsonConstructor]
    private DailyPlan() { } // For deserialization

    public DailyPlan(DateOnly dateFor)
    {
        Id = Guid.NewGuid();
        DateFor = dateFor;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetTaskPriorities(IEnumerable<Guid> orderedTaskIds)
    {
        OrderedTaskIds.Clear();
        OrderedTaskIds.AddRange(orderedTaskIds);
    }

    public void AddNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Note cannot be empty.", nameof(note));

        Notes.Add(note.Trim());
    }
}
