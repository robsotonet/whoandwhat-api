namespace WhoAndWhat.Domain.Events;

public class UserDeactivatedEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserDeactivatedEvent(Guid userId)
    {
        UserId = userId;
    }
}
