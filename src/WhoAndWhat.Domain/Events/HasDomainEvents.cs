using System.Collections.Generic;

namespace WhoAndWhat.Domain.Events;

public abstract class HasDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = new();
    public IEnumerable<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
