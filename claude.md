# WhoAndWhat - Smart Task Management API

## Project Overview
WhoAndWhat is a bilingual (English/Spanish), AI-powered task management platform with social connectivity features. This repository contains the .NET Core Web API backend that serves both web and mobile clients.

## Core Features
- [x] Project setup and architecture planning
- [ ] Authentication & User Management (JWT, OAuth 2.0)
- [ ] Task Management System (Categories: To-Dos, Ideas, Appointments, Bill Reminders, Projects)
- [ ] Contact Management & Linking
- [ ] Dashboard & Analytics
- [ ] AI Planning Integration
- [ ] Calendar Management
- [ ] Real-time Updates (SignalR)
- [ ] Bilingual Support (English/Spanish)
- [ ] Event Discovery (Future Feature)

## Technology Stack
- **Backend**: ASP.NET Core 9.0 Web API
- **Database**: PostgreSQL
- **Real-time**: SignalR
- **Authentication**: JWT with OAuth 2.0 (Google, Facebook, Apple)
- **Containerization**: Docker (local), Azure Container Technologies (prod)
- **Security**: Azure Key Vault, encryption at rest/transit
- **Testing**: xUnit with 80%+ coverage requirement

## Architecture
Following Clean Architecture principles:
- **Domain Layer**: Entities, value objects, domain services
- **Application Layer**: Use cases, DTOs, interfaces
- **Infrastructure Layer**: Database, external services, repositories
- **Presentation Layer**: Controllers, SignalR hubs, middleware

## Development Workflow
1. **Feature Development**
   - Create feature branch
   - Implement with TDD approach
   - Write comprehensive unit tests (80%+ coverage)
   - Update this document with progress
   
2. **Code Standards**
   - Follow C# best practices and naming conventions
   - Use async/await for all I/O operations
   - Implement proper error handling and logging
   - Document complex business logic
   - Follow SOLID principles

3. **Review Process**
   - Code review required before merge
   - Unit tests must pass
   - Coverage requirements must be met

## Project Structure
```
src/
├── WhoAndWhat.Domain/          # Domain entities, value objects
├── WhoAndWhat.Application/     # Use cases, DTOs, interfaces
├── WhoAndWhat.Infrastructure/  # Database, repositories, external services
├── WhoAndWhat.API/            # Controllers, middleware, startup
└── WhoAndWhat.Tests/          # Unit and integration tests
```

## Current Progress

### Phase 1: Foundation (In Progress)
- [x] Project requirements analysis
- [x] Architecture design
- [ ] Project structure setup
- [ ] Database schema design
- [ ] Docker configuration
- [ ] CI/CD pipeline setup

### Phase 2: Core Authentication
- [ ] User registration/login
- [ ] JWT token management
- [ ] OAuth 2.0 integration (Google, Facebook, Apple)
- [ ] Password reset functionality
- [ ] Account verification

### Phase 3: Task Management Core
- [ ] Task CRUD operations
- [ ] Category management (To-Dos, Ideas, Appointments, Bill Reminders, Projects)
- [ ] Task-to-project conversion
- [ ] Priority and due date management
- [ ] Task search and filtering

### Phase 4: Contact & Social Features
- [ ] Contact management
- [ ] QR code generation/scanning
- [ ] Invite code system
- [ ] Task-contact linking
- [ ] Shared task visibility

### Phase 5: Dashboard & Analytics
- [ ] Task completion metrics
- [ ] Productivity streaks
- [ ] Overdue task tracking
- [ ] Motivational content system

### Phase 6: AI & Calendar Integration
- [ ] AI planning algorithms
- [ ] Calendar view (day/week/month)
- [ ] Smart scheduling
- [ ] Time block suggestions
- [ ] Break recommendations

### Phase 7: Real-time & Notifications
- [ ] SignalR hub implementation
- [ ] Real-time task updates
- [ ] Push notification system
- [ ] Reminder system

### Phase 8: Localization & Configuration
- [ ] Bilingual support (English/Spanish)
- [ ] User preferences
- [ ] Theme customization
- [ ] Notification settings

## Database Schema (Planned)
### Core Entities
- **Users**: Authentication, preferences, localization
- **Tasks**: Core task management with categories
- **Contacts**: User contact relationships
- **Projects**: Task groupings with subtasks
- **TaskContacts**: Many-to-many relationship
- **Dashboard**: User metrics and preferences
- **Events**: Future feature for event discovery

## API Endpoints (Planned)
```
Authentication:
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout

Tasks:
GET    /api/tasks
POST   /api/tasks
PUT    /api/tasks/{id}
DELETE /api/tasks/{id}
POST   /api/tasks/{id}/convert-to-project

Contacts:
GET    /api/contacts
POST   /api/contacts
POST   /api/contacts/invite
POST   /api/contacts/qr-scan

Dashboard:
GET    /api/dashboard/metrics
GET    /api/dashboard/motivation

AI Planning:
POST   /api/ai/plan-day
GET    /api/ai/suggestions

Calendar:
GET    /api/calendar/{year}/{month}
GET    /api/calendar/week/{date}
```

## Security Considerations
- JWT token management with refresh tokens
- OAuth 2.0 integration with major providers
- Encryption at rest and in transit
- PII data handling compliance
- Secure file upload validation
- API key management with Azure Key Vault
- Audit logging for sensitive operations
- Rate limiting and DDoS protection

## Monitoring & Observability
- Response time tracking
- Error rate monitoring
- Database performance metrics
- Resource utilization alerts
- Application Insights integration

## Testing Strategy
- Unit tests for all business logic (80%+ coverage)
- Integration tests for API endpoints
- End-to-end tests for critical user flows
- Load testing for performance validation

## Deployment
- **Local**: Docker Compose
- **Production**: Azure Container Instances/App Service
- **Database**: Azure Database for PostgreSQL
- **Secrets**: Azure Key Vault
- **Monitoring**: Azure Application Insights

## Next Steps
1. Set up project structure with Clean Architecture
2. Configure Docker development environment
3. Design and implement database schema
4. Create basic authentication system
5. Implement core task management features

## Notes
- Keep this document updated with current progress
- Use checkboxes to track completed features
- Document any deviations from the original plan
- Include relevant code snippets and examples for complex implementations