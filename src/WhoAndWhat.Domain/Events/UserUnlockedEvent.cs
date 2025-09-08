namespace WhoAndWhat.Domain.Events;

public class UserUnlockedEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserUnlockedEvent(Guid userId)
    {
        UserId = userId;
    }
}
