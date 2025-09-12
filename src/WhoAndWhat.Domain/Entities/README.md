# Domain Entities

This folder contains the core business entities of the WhoAndWhat application.

## Purpose
Domain entities represent the main business objects and encapsulate business logic and rules. They should:
- Be independent of external frameworks
- Contain business logic and validation
- Raise domain events when appropriate
- Follow rich domain model principles

## Planned Entities
- **User**: User account and authentication information
- **Task**: Core task entity with categories, priorities, and status
- **Project**: Project entity for grouping related tasks
- **Contact**: User contact information and relationships
- **Event**: Calendar events and scheduling (future feature)

## Guidelines
- Use private setters and domain methods to maintain invariants
- Implement domain events for cross-aggregate communication
- Keep entities focused on their core responsibilities
- Avoid anemic domain models