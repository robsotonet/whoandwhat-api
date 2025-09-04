namespace WhoAndWhat.Domain.Events;

public class UserEmailVerifiedEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserEmailVerifiedEvent(Guid userId)
    {
        UserId = userId;
    }
}