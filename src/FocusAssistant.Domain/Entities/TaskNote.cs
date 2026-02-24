using System.Text.Json.Serialization;

namespace FocusAssistant.Domain.Entities;

/// <summary>
/// A timestamped piece of context attached to a FocusTask.
/// Parent task reference is optional to support standalone notes.
/// </summary>
public sealed class TaskNote
{
    public Guid Id { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? TaskId { get; private set; }

    [JsonConstructor]
    private TaskNote() { Content = string.Empty; } // For deserialization

    public TaskNote(string content, Guid? taskId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Note content cannot be empty.", nameof(content));

        Id = Guid.NewGuid();
        Content = content.Trim();
        CreatedAt = DateTime.UtcNow;
        TaskId = taskId;
    }

    public void AttachToTask(Guid taskId)
    {
        TaskId = taskId;
    }

    public bool IsStandalone => !TaskId.HasValue;
}
