# Application Interfaces

This folder contains interfaces that define contracts for external dependencies.

## Purpose
Interfaces enable:
- Dependency inversion principle
- Testability with mocks
- Plugin architecture
- Clean boundaries between layers

## Planned Interfaces
- **Repositories**: Data access contracts (IUserRepository, ITaskRepository)
- **Services**: External service contracts (IEmailService, IFileStorageService)
- **Infrastructure**: Infrastructure service contracts (ICacheService, IQueueService)
- **External APIs**: Third-party service contracts (IAIService, ICalendarService)

## Guidelines
- Define focused, cohesive interfaces
- Use async/await for I/O operations
- Return Result<T> for error handling
- Include comprehensive XML documentation
- Follow ISP (Interface Segregation Principle)