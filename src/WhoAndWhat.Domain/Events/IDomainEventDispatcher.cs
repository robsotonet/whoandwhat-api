namespace WhoAndWhat.Domain.Events;

public interface IDomainEventDispatcher
{
    public Task Dispatch(DomainEvent domainEvent);
}
