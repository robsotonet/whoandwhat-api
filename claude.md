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

### Phase 1: Foundation (COMPLETED ✅)
- [x] Project requirements analysis
- [x] Architecture design
- [x] Project structure setup (Complete Clean Architecture implementation)
- [x] Database schema design (Entity models with EF Core configuration)
- [x] Docker configuration (PostgreSQL, Redis, pgAdmin setup)
- [x] CI/CD pipeline setup (Comprehensive Azure DevOps pipeline)

**Bonus Features Completed:**
- [x] API Foundation (Health checks, API versioning, Swagger documentation)
- [x] Structured logging (Serilog configuration)
- [x] Security middleware (Security headers, global exception handling)
- [x] Repository pattern with MediatR for CQRS
- [x] Response compression and CORS configuration
- [x] Application Insights integration
- [x] Database seeding infrastructure
- [x] Comprehensive testing framework (7 test projects)

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

## 🚀 READY FOR PHASE 2: Core Authentication

**Current Status**: Phase 1 COMPLETE ✅ - All foundation work finished and verified.

### Immediate Next Steps (High Priority)

**Phase 2 is ready to begin immediately** - All foundational infrastructure is in place:
- ✅ JWT configuration prepared in environment 
- ✅ OAuth providers configured (Google, Facebook, Apple)
- ✅ User domain model foundation established
- ✅ Repository patterns implemented
- ✅ Authentication middleware prepared (commented out)
- ✅ Comprehensive technical specifications documented in `tasks/phase-2-auth/README.md`
- ✅ Placeholder tests created for TDD approach

### Recommended Development Workflow

1. **Start with Authentication Infrastructure** (3-4 days)
   - Implement JWT token service with refresh token rotation
   - Enable authentication middleware in `ApplicationBuilderExtensions.cs:58`
   - Create JWT configuration service
   - Set up password hashing with BCrypt

2. **Implement Core Auth Endpoints** (2-3 days)
   - Create `AuthController` with register/login/logout endpoints
   - Implement password validation and user registration
   - Add refresh token functionality
   - Enable placeholder tests in `AuthControllerTests.cs`

3. **Add Password Management** (2 days)
   - Implement password reset functionality
   - Set up email verification service
   - Configure SMTP for development

4. **OAuth Integration** (3-4 days)
   - Enable OAuth providers (Google, Facebook, Apple)
   - Implement OAuth callback handlers
   - Create user account linking logic

5. **Security Hardening** (2 days)
   - Enable rate limiting middleware
   - Add account lockout functionality
   - Implement security headers
   - Run security validation tests

### Quick Start Commands

```bash
# Verify current foundation
dotnet build --configuration Release
dotnet test tests/WhoAndWhat.Domain.Tests/ tests/WhoAndWhat.Application.Tests/

# Start development environment
docker-compose up -d db redis

# Run API for testing
dotnet run --project src/WhoAndWhat.API/
```

### Architecture Highlights

The project is built with **Clean Architecture** and modern .NET practices:
- **Domain Layer**: Rich domain models with business logic
- **Application Layer**: CQRS with MediatR, use cases, DTOs  
- **Infrastructure Layer**: EF Core, repositories, external services
- **API Layer**: Controllers, middleware, API versioning, Swagger

### Key Resources

- **Phase 2 Technical Specs**: `tasks/phase-2-auth/README.md` (comprehensive implementation guide)
- **API Documentation**: Available at `/swagger` when running locally
- **Docker Setup**: Full development environment with PostgreSQL, Redis, pgAdmin
- **CI/CD Pipeline**: Azure DevOps pipeline configured and ready

## Notes
- Keep this document updated with current progress
- Use checkboxes to track completed features
- Document any deviations from the original plan
- Include relevant code snippets and examples for complex implementations