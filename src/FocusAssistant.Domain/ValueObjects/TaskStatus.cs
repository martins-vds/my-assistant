namespace FocusAssistant.Domain.ValueObjects;

/// <summary>
/// Represents the lifecycle status of a FocusTask.
/// </summary>
public enum TaskStatus
{
    InProgress,
    Paused,
    Completed,
    Archived
}
