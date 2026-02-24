namespace FocusAssistant.Domain.Events;

public sealed record TaskSwitchedEvent(Guid PreviousTaskId, string PreviousTaskName) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
