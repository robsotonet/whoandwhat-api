# Domain Events

This folder contains domain events that represent significant business occurrences.

## Purpose
Domain events enable:
- Loose coupling between bounded contexts
- Side effects and business workflows
- Integration with external systems
- Audit trails and event sourcing

## Planned Events
- **UserCreatedEvent**: When a new user registers
- **TaskCreatedEvent**: When a task is created
- **TaskCompletedEvent**: When a task is completed
- **TaskSharedEvent**: When a task is shared with contacts
- **ProjectCreatedEvent**: When a project is created

## Guidelines
- Events should be immutable
- Include all relevant data for event handlers
- Use past tense naming (UserCreated, not CreateUser)
- Keep events focused and specific