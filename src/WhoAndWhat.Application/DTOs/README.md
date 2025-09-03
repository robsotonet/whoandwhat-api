# Data Transfer Objects (DTOs)

This folder contains DTOs for data transfer between application layers.

## Purpose
DTOs provide:
- Clean separation between internal and external representations
- API versioning support
- Input validation and sanitization
- Response formatting

## Structure
- **Requests**: Input DTOs for commands and queries
- **Responses**: Output DTOs for API responses
- **Commands**: Command DTOs for write operations
- **Queries**: Query DTOs for read operations

## Planned DTOs
- **Authentication**: Login, registration, password reset DTOs
- **Tasks**: Task creation, update, filtering DTOs
- **Projects**: Project management DTOs
- **Contacts**: Contact and sharing DTOs
- **Dashboard**: Analytics and metrics DTOs

## Guidelines
- Use record types for immutability
- Include validation attributes
- Provide meaningful property names
- Support both English and Spanish localization