# AI-Assisted Software Engineering System - URLShortener

## 1. Purpose

Build a secure, maintainable URL-shortening application with an Angular dashboard, an ASP.NET Core 8 Web API, and PostgreSQL persistence. Users can create short links, inspect link details and click analytics, and visitors can follow short links to their destinations.

## 2. Technology and Architecture

- Frontend: Angular and TypeScript.
- API and service layer: ASP.NET Core 8 with controller-based endpoints and SwaggerGen/OpenAPI.
- Database: PostgreSQL.
- Data access: Entity Framework Core, `DbContext`, LINQ, and parameterized queries.
- Tests: xUnit unit and integration tests.
- Architecture: loosely coupled layers with dependencies pointing inward. Keep domain rules independent of EF Core, ASP.NET Core, and Angular.
- Dependency injection is used for application services, repositories, and persistence.
- CORS allows only configured frontend origins.

## 3. Project Structure

```text
src/
  UrlShortener.Domain/          Domain entities and business rules
  UrlShortener.Data/             EF Core DbContext, mappings, migrations
  UrlShortener.Infrastructure/   Repository implementations and persistence adapters
  UrlShortener.Service/           ASP.NET Core API, controllers, DTOs, application services
frontend/
  url-shortener-ui/              Angular dashboard
tests/
  UrlShortener.UnitTests/        xUnit domain and service tests
  UrlShortener.IntegrationTests/ xUnit API and PostgreSQL integration tests
database/
  UrlShortener.sql               Reviewed PostgreSQL schema and operational queries
```

The API host is `UrlShortener.Service`. Infrastructure implements repository contracts owned by the inner layers. The Data project owns EF Core persistence configuration and migrations; it does not own business rules.

## 4. Functional Requirements

1. Create a short URL from an absolute HTTP or HTTPS destination.
2. Generate URL-safe, cryptographically random short codes and enforce uniqueness in PostgreSQL.
3. Redirect `GET /{code}` to the active destination with HTTP 302. Do not use permanent redirects because they may be cached and bypass click tracking.
4. Retrieve URL details by code, including creation time, expiry, active state, and click count.
5. Record click events with UTC access time and expose analytics filtered by a UTC date range.
6. Reject malformed, unsupported-scheme, expired, or disabled links with documented status codes.
7. Provide an Angular dashboard to create links, copy short URLs, list links, view details, and inspect click analytics.

## 5. API Contract

| Method | Route | Behavior |
|---|---|---|
| `POST` | `/api/urls` | Validate and create a short URL; return `201 Created`. |
| `GET` | `/api/urls/{code}` | Return URL details; return `404` if not found. |
| `GET` | `/api/urls/{code}/analytics?fromUtc=&toUtc=` | Return click totals and time-bucketed counts. |
| `GET` | `/{code}` | Record a click and redirect with `302 Found`; return `404` for unknown, expired, or inactive links. |
| `GET` | `/health` | Report application health without exposing secrets. |

Use dedicated request and response DTOs; never serialize EF entities directly. Use consistent problem-details responses for API errors. Timestamps use UTC and `DateTimeOffset` at API boundaries.

## 6. Database and Normalization

Use a Code First workflow: entity configurations and EF Core migrations are the schema source of truth. Generate and review deployment SQL from migrations. Keep the resulting PostgreSQL DDL and reviewed operational queries in `database/UrlShortener.sql`; update it whenever the schema changes. Do not use database-first scaffolding to define the schema.

The initial relational model is in Third Normal Form (3NF):

- `short_urls`: one row per short link; primary key, unique code, destination URL, created/expiry timestamps, active state.
- `click_events`: one row per click; primary key, foreign key to `short_urls`, access timestamp, and optional privacy-reviewed metadata. Do not duplicate URL code or destination in each event.

Use a unique index on `short_urls.code` and an index on `(click_events.short_url_id, click_events.occurred_at_utc)` for analytics. Use PostgreSQL UTC-compatible timestamps. Add triggers or stored procedures only for a documented requirement that cannot be handled suitably by application logic or EF Core.

Application reads and writes use EF Core and LINQ by default. Any necessary raw SQL must be parameterized, reviewed, and represented in `UrlShortener.sql`; never concatenate user input into SQL.

## 7. Security and Reliability

- Accept only absolute `http` and `https` URLs. Set input length limits and normalize/validate before persistence.
- The redirect service does not fetch destination URLs. Any future URL inspection feature must mitigate SSRF, including private, loopback, and link-local destinations.
- Rate-limit link creation and protect management and analytics data with authorization if user accounts are introduced.
- Apply database uniqueness constraints; retry short-code collisions safely.
- Analytics failures must not prevent an otherwise valid redirect. Document whether click capture is best-effort or durable; production durability requires a durable queue or database-backed outbox.
- Do not log secrets, full sensitive query strings, or unneeded personal data. Define retention for click-event metadata.
- Use explicit CORS origin configuration and secure production settings.

## 8. Error Handling and Coding Rules

- Follow `CONSTITUTION.md` as persistent project guidance.
- Each method in the DAL and service layer uses `try/catch`; catches must log, translate, recover, or rethrow and must never silently swallow failures.
- Keep controllers thin: validate/translate HTTP input and delegate behavior to application services.
- Use async EF Core APIs and cancellation tokens for I/O operations.
- Keep methods focused, use clear names, and avoid leaking internal exception details to clients.
- Use dependency injection and abstractions to keep layers testable and replaceable.

## 9. Testing and Acceptance Criteria

- Unit tests cover URL validation, code generation/collision behavior, expiry rules, and analytics aggregation.
- Integration tests cover API status codes and headers, EF Core mappings, PostgreSQL constraints, click recording, and analytics filters.
- Tests cover invalid schemes, malformed URLs, unknown codes, expired/disabled links, database failures, and analytics failure behavior.
- The solution builds, tests pass, Swagger describes the API, and the Angular dashboard can call the configured API origin.
- PostgreSQL integration tests use a real PostgreSQL instance or Testcontainers; EF Core InMemory is not a substitute for relational behavior.