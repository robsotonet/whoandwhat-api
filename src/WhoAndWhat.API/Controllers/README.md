# API Controllers

This folder contains REST API controllers for all endpoints.

## Purpose
Controllers handle:
- HTTP request/response processing
- Input validation and model binding
- Authentication and authorization
- API documentation and versioning

## Planned Controllers
- **AuthController**: Authentication endpoints (login, register, OAuth)
- **TasksController**: Task CRUD and management endpoints
- **ProjectsController**: Project management endpoints
- **ContactsController**: Contact and sharing endpoints
- **DashboardController**: Analytics and dashboard endpoints
- **AIController**: AI planning and optimization endpoints

## Guidelines
- Use API versioning (v1, v2, etc.)
- Include comprehensive Swagger documentation
- Implement proper HTTP status codes
- Use async/await for all operations
- Include input validation attributes
- Follow RESTful conventions