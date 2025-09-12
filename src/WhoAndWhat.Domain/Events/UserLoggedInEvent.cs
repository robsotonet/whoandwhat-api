namespace WhoAndWhat.Domain.Events;

public class UserLoggedInEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserLoggedInEvent(Guid userId)
    {
        UserId = userId;
    }
}
