namespace WhoAndWhat.Domain.Events;

public class UserPreferencesUpdatedEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserPreferencesUpdatedEvent(Guid userId)
    {
        UserId = userId;
    }
}
