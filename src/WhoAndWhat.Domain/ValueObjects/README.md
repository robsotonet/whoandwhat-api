# Domain Value Objects

This folder contains value objects that represent concepts in the business domain.

## Purpose
Value objects are immutable objects that represent a descriptive aspect of the domain with no conceptual identity.

## Planned Value Objects
- **Priority**: Task priority levels (1-5)
- **TaskCategory**: Categories like ToDo, Idea, Appointment, BillReminder, Project
- **TaskStatus**: Status enumeration (Pending, InProgress, Completed, Cancelled)
- **Language**: Supported language codes (en, es)
- **ContactRelationType**: Types of contact relationships

## Guidelines
- Implement value equality (override Equals and GetHashCode)
- Make objects immutable
- Include validation in constructors
- Provide meaningful ToString() implementations