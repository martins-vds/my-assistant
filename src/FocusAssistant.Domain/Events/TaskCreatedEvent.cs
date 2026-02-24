namespace FocusAssistant.Domain.Events;

public sealed record TaskCreatedEvent(Guid TaskId, string TaskName) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
