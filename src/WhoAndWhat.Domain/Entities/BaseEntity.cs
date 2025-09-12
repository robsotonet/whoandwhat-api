using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

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

    /// <summary>
    /// Soft deletes the entity by marking it as deleted
    /// </summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft deleted entity
    /// </summary>
    public virtual void Restore()
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the entity can be soft deleted
    /// </summary>
    /// <returns>True if the entity can be soft deleted</returns>
    public virtual bool CanSoftDelete()
    {
        return !IsDeleted;
    }

    /// <summary>
    /// Checks if the entity can be restored
    /// </summary>
    /// <returns>True if the entity can be restored</returns>
    public virtual bool CanRestore()
    {
        return IsDeleted;
    }
}
