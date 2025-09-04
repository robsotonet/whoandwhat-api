# Domain Services

This folder contains domain services that encapsulate business logic that doesn't naturally fit within a single entity.

## Purpose
Domain services handle:
- Business logic that spans multiple entities
- Complex business rules and calculations
- Domain operations that require coordination between entities

## Planned Services
- **TaskDomainService**: Task-related business operations
- **UserDomainService**: User management business logic
- **ProjectDomainService**: Project management operations
- **ContactDomainService**: Contact relationship management

## Guidelines
- Keep services stateless
- Use dependency injection for external dependencies
- Return domain results rather than throwing exceptions
- Focus on business logic, not data access