# Gemini - Project Analysis & Strategy for WhoAndWhat API

## 1. Project Understanding

Based on my analysis of `claude.md` and `design_system.json`, I understand that the "WhoAndWhat" project is a sophisticated, bilingual (English/Spanish) task management API. It's built on an ASP.NET Core 9.0 backend, designed with a Clean Architecture pattern, and intended to serve both web and mobile clients. The platform integrates AI-powered planning, social connectivity through contact management, and real-time updates using SignalR.

## 2. Core Objective

The primary goal is to develop a robust, scalable, and secure backend API that delivers the following key user-facing outcomes:
-   **Smart Task Management**: Categorize and manage various life tasks (To-Dos, Ideas, Appointments, Bills, Projects).
-   **Social Connectivity**: Manage contacts and share tasks.
-   **AI-Powered Planning**: Leverage AI to help users plan their days and schedules.
-   **Real-time Experience**: Ensure data is synchronized across all user devices instantly.

## 3. Key Features & Scope

I have identified the following core features to be implemented, as detailed in the project documentation:

-   **Authentication**: JWT-based with OAuth 2.0 for Google, Facebook, and Apple.
-   **Task System**: Full CRUD operations for tasks, including categorization, prioritization, and project conversion.
-   **Contact Management**: User contacts, QR code sharing, and an invitation system.
-   **AI & Calendar**: AI-driven daily planning, smart scheduling, and calendar views.
-   **Real-time Updates**: SignalR hubs for tasks and notifications.
-   **Localization**: Full support for English and Spanish.
-   **Dashboard & Analytics**: User productivity metrics and motivational content.

## 4. Technology & Architecture

I will adhere strictly to the established technology stack and architectural principles.

-   **Architecture**: I will follow the Clean Architecture pattern, ensuring a clear separation of concerns between the **Domain**, **Application**, **Infrastructure**, and **Presentation** layers. All my code contributions will respect these boundaries.
-   **Technology Stack**:
    -   **Backend**: ASP.NET Core 9.0 with C# 13.
    -   **Database**: PostgreSQL with Entity Framework Core for data access.
    -   **Real-time**: SignalR for instant updates.
    -   **Testing**: xUnit and Moq, with a commitment to maintaining >= 80% test coverage.
    -   **Containerization**: Docker for the local development environment.
    -   **Deployment**: Azure services (App Service, PostgreSQL, Key Vault).

## 5. My Role & Approach

My role is to act as an AI software engineering assistant to help build, test, and document this API. I will perform tasks by:

1.  **Analyzing Requests**: Understanding the specific requirements for any given task.
2.  **Adhering to Conventions**: Following the project's coding standards, naming conventions (e.g., `PascalCase` for DTOs), and architectural patterns.
3.  **Test-Driven Development (TDD)**: Writing or updating unit tests with xUnit and Moq before or alongside feature implementation to ensure correctness and maintain the 80% coverage standard.
4.  **Secure Coding**: Implementing security best practices, especially regarding authentication, data protection (PII), and secret management (Azure Key Vault).
5.  **Documentation**: Keeping documentation, including this file, updated as the project evolves.

## 6. Development Strategy

I will tackle development in a phased approach, as outlined in the project plan.

-   **Phase 1: Foundation**: I will start by helping to flesh out the project structure, setting up configurations, and defining the initial database schema based on the domain model in `design_system.json`.
-   **Subsequent Phases**: I will proceed to implement features for Authentication, Task Management, Contacts, and so on, ensuring each piece of functionality is well-tested and integrated.
-   **API Versioning**: All endpoints I create or modify will conform to the `/api/v1/` versioning scheme.
-   **Error Handling & Logging**: I will implement structured responses for success and errors and integrate Serilog for structured logging as specified.

## 7. Next Steps

Based on the current progress, the immediate next steps are:

1.  **Finalize Project Structure**: Create the solution and project files for all four layers (`Domain`, `Application`, `Infrastructure`, `API`).
2.  **Define Domain Entities**: Code the core entities (`User`, `Task`, `Contact`, `Project`) in the `WhoAndWhat.Domain` project.
3.  **Database Setup**: Configure Entity Framework Core, create the initial database migration, and set up seeding for development.
4.  **Implement Basic Endpoints**: Create a health check endpoint to verify the API is running correctly.

I am ready to begin with the first task.
