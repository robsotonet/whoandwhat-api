# Application Use Cases

This folder contains use case implementations that orchestrate business operations.

## Purpose
Use cases represent application-specific business rules and coordinate the flow of data to and from entities.

## Planned Use Cases
- **Authentication**: User registration, login, password management
- **TaskManagement**: Create, update, delete, search tasks
- **ProjectManagement**: Create projects, manage task assignments
- **ContactManagement**: Manage contacts, sharing, invitations
- **Dashboard**: Analytics, metrics, motivational content

## Guidelines
- One use case per business operation
- Use Command/Query separation (CQRS)
- Return Result<T> for error handling
- Keep use cases focused and single-purpose
- Coordinate with domain services and repositories