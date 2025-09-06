using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    // Protected constructor for testing purposes
    protected BaseEntity()
    {
    }

    // Protected constructor to allow setting Id in derived classes for testing
    protected BaseEntity(Guid id, DateTime? createdAt = null)
    {
        Id = id;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void MarkAsModified()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}