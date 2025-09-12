namespace WhoAndWhat.Domain.Events;

public class UserPasswordChangedEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserPasswordChangedEvent(Guid userId)
    {
        UserId = userId;
    }
}
