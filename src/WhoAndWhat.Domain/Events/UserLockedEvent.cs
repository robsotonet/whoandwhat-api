namespace WhoAndWhat.Domain.Events;

public class UserLockedEvent : DomainEvent
{
    public Guid UserId { get; }
    public DateTime LockedUntil { get; }

    public UserLockedEvent(Guid userId, DateTime lockedUntil)
    {
        UserId = userId;
        LockedUntil = lockedUntil;
    }
}
