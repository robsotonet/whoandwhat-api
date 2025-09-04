# Infrastructure Services

This folder contains implementations of external services and infrastructure concerns.

## Purpose
Infrastructure services handle:
- Email sending and templates
- File storage and management
- Caching (Redis)
- Message queuing
- External API integrations

## Planned Services
- **EmailService**: SMTP/SendGrid email delivery
- **FileStorageService**: Azure Blob/local file storage
- **CacheService**: Redis caching implementation
- **QueueService**: Message queue for background jobs
- **NotificationService**: Push notification delivery
- **QRCodeService**: QR code generation and validation

## Guidelines
- Implement application interfaces
- Handle external service failures gracefully
- Include proper logging and monitoring
- Use circuit breaker pattern for external calls
- Implement retry policies with exponential backoff