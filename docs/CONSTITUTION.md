# Project Constitution

These rules govern the design and implementation of the URL shortener. They remain in force throughout development. Do not replace or weaken them without the user's explicit approval.

## Architecture and Technology

1. Use a loosely coupled Clean Architecture. Keep domain rules independent of infrastructure and frameworks, and make dependencies point inward. Separate the Angular presentation, ASP.NET Core API, application/service, domain, and infrastructure/data-access responsibilities.
2. Use Angular for the web frontend and ASP.NET Core 8 for the HTTP API and service layer.
3. Use PostgreSQL as the relational database and Entity Framework Core as the data-access layer.
4. Use xUnit for automated unit and integration tests.
5. Use ASP.NET Core controllers for HTTP endpoints. Do not mix controller and minimal API endpoint styles.
6. Use SwaggerGen/OpenAPI to document and expose the API specification.

## Data and Persistence

1. Use the EF Core `DbContext` for persistence and LINQ for database queries. Keep query and persistence logic in the data-access/infrastructure layer; do not expose EF entities directly as API contracts.
2. Follow a Code First workflow: define and evolve the schema through EF Core entities, configuration, and migrations. Generate reviewable SQL scripts from migrations for database deployment. Do not use database-first scaffolding as the schema source of truth.
3. Add database triggers or stored procedures only when a documented requirement justifies them and the behavior cannot be handled appropriately in the application or EF Core. Keep their definitions and deployment in version control.
4. Use dependency injection for application services, repositories/data-access components, and other dependencies. Depend on abstractions across architectural boundaries where appropriate.

## Implementation Standards

1. Follow established C# and Angular coding conventions, use clear names, keep methods focused, and validate external input at system boundaries.
2. Every method in the data-access and service layers must use `try/catch` as required by the project. Catch exceptions only to add meaningful handling, logging, or translation; rethrow when the current layer cannot recover. Never silently swallow exceptions or expose sensitive implementation details to API clients.
3. Use consistent API error responses and appropriate HTTP status codes. Keep transport concerns in controllers and business rules in the application/domain layers.
4. Configure CORS with explicit allowed origins. Do not allow every origin in production.
5. Validate submitted destination URLs and permit only supported schemes (`http` and `https`). Apply appropriate abuse protections to URL creation and redirects.

## Verification and Change Control

1. Add or update xUnit tests for behavior changes. Cover application logic with unit tests and verify API, EF Core, and PostgreSQL behavior with integration tests where applicable.
2. Run the relevant tests and build checks before considering a change complete.
3. Keep migrations, generated deployment SQL, and application changes consistent and reviewable; do not make untracked manual schema changes the source of truth.
4. Treat this constitution as persistent project guidance. Amendments require the user's explicit approval.