# Application Services

This folder contains application services that orchestrate business operations.

## Purpose
Application services:
- Coordinate use case execution
- Handle application-level concerns
- Manage transaction boundaries
- Transform between DTOs and domain objects

## Planned Services
- **AuthenticationService**: User authentication workflows
- **TaskService**: Task management operations
- **ProjectService**: Project management workflows
- **ContactService**: Contact and sharing operations
- **DashboardService**: Analytics and dashboard data
- **NotificationService**: User notification management

## Guidelines
- Services should be stateless
- Use dependency injection for repositories and external services
- Handle cross-cutting concerns (logging, caching, validation)
- Implement proper error handling and logging
- Follow single responsibility principle