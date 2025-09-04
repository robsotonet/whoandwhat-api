# Infrastructure Data

This folder contains database context, configurations, and data access infrastructure.

## Purpose
Data layer provides:
- Database context and configuration
- Entity Framework migrations
- Database seeding and initialization
- Connection management

## Planned Components
- **WhoAndWhatDbContext**: Main Entity Framework context
- **Configurations**: Entity configurations using Fluent API
- **Migrations**: Database schema migrations
- **Interceptors**: EF Core interceptors for auditing, performance
- **Seed**: Development and test data seeding

## Guidelines
- Use Fluent API for entity configuration
- Implement proper connection string management
- Use connection pooling for performance
- Include audit fields (CreatedAt, UpdatedAt)
- Follow EF Core best practices