namespace FocusAssistant.Domain.Events;

public sealed record TaskCompletedEvent(Guid TaskId, string TaskName) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
