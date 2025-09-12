# Domain Events Implementation Strategy

## Overview

This document outlines the strategy for implementing domain events in the WhoAndWhat API project.

## Current Status

The domain event infrastructure has been prepared but is not yet actively used:

- ✅ **Domain Event Base Class**: `WhoAndWhat.Domain.Events.DomainEvent` 
- ✅ **Event Dispatcher Interface**: `WhoAndWhat.Domain.Events.IDomainEventDispatcher`
- ✅ **MediatR Integration**: `WhoAndWhat.Infrastructure.Services.DomainEventDispatcher`
- ✅ **MediatR Registration**: Configured in `Program.cs` for future use
- ⏸️ **ApplicationDbContext**: Simplified constructor without dispatcher dependency (temporarily)

## Implementation Approach

### Phase 1: Entity-Level Domain Events (Future)
1. **Add domain event support to entities**:
   - Create base `Entity` class with domain event collection
   - Implement `AddDomainEvent(DomainEvent @event)` method
   - Modify domain entities to inherit from base Entity class

2. **Update ApplicationDbContext**:
   - Re-add `IDomainEventDispatcher` parameter to constructor
   - Implement domain event dispatching in `SaveChangesAsync`
   - Dispatch events before saving changes

### Phase 2: Business Logic Events (Future)
Define domain events for key business operations:

- **User Events**:
  - `UserRegisteredEvent`
  - `UserLoginEvent`
  
- **Task Events**:
  - `TaskCreatedEvent`
  - `TaskCompletedEvent`
  - `TaskAssignedToProjectEvent`
  
- **Project Events**:
  - `ProjectCreatedEvent`
  - `TaskAddedToProjectEvent`

### Phase 3: Event Handlers (Future)
Implement event handlers for cross-cutting concerns:

- **Audit Logging**: Track all domain events for compliance
- **Notifications**: Send notifications when tasks are assigned
- **Analytics**: Collect metrics on user behavior
- **Integration**: Sync with external systems

## Code Example (Future Implementation)

```csharp
// Entity Base Class
public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = new();
    
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// ApplicationDbContext with Event Dispatching
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var entities = ChangeTracker.Entries<Entity>()
        .Where(e => e.Entity.DomainEvents.Any())
        .Select(e => e.Entity)
        .ToList();

    foreach (var entity in entities)
    {
        foreach (var domainEvent in entity.DomainEvents)
        {
            await _dispatcher.Dispatch(domainEvent);
        }
        entity.ClearDomainEvents();
    }

    return await base.SaveChangesAsync(cancellationToken);
}
```

## Migration Notes

When implementing domain events:

1. **Restore IDomainEventDispatcher registration** in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
   ```

2. **Update ApplicationDbContext constructor**:
   ```csharp
   public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IDomainEventDispatcher dispatcher) : base(options)
   {
       _dispatcher = dispatcher;
   }
   ```

3. **Update test files** to provide IDomainEventDispatcher mock when creating ApplicationDbContext

## Benefits

- **Decoupling**: Separate domain logic from infrastructure concerns
- **Extensibility**: Easy to add new event handlers without modifying existing code
- **Testability**: Events can be tested independently
- **Auditability**: Complete history of domain changes
- **Integration**: Clean way to integrate with external systems

## References

- [Domain Events Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Clean Architecture Domain Events](https://blog.ploeh.dk/2021/05/03/the-lazy-applicative-functor/)