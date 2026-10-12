# URLShortener Prototype: Architecture, Scenarios, Setup, and Testing

Written for: engineers joining the project who need to run it, change it, and judge how far the prototype can be trusted.

Verified on 2026-10-12 with .NET SDK 10.0.401 (targets `net8.0`), Node with npm, Docker 28.4.0, and a Windows PostgreSQL 18 service on port 5432. Updated the same day after the refresh and chart-date fixes (Section 8).

Related documents: `SPEC.md` (requirements), `CONSTITUTION.md` (rules that apply to every change), `README.md` (quick start), `PLAN.md` (phased plan), `TASK.md` (ordered task list with status).

## 1. Working Prototype

The prototype is the URL shortener itself: an Angular dashboard, an ASP.NET Core 8 API, and PostgreSQL. A user can create a short link, follow it (302 redirect, click recorded), see click totals, and browse links.

End-to-end checks run against the live stack on this date:

| Step | Request | Result |
|---|---|---|
| Health | `GET /health` | `200 {"status":"healthy"}` |
| Create | `POST /api/urls` with `https://example.com/docs/e2e-check` | `201 Created`, 8-character code |
| Follow | `GET /{code}` | `302` to the destination; click recorded |
| Details | `GET /api/urls/{code}` | `clickCount: 1` |
| Analytics | `GET /api/urls/{code}/analytics` | `totalClicks: 1`, one daily bucket for today (UTC) |
| Reject scheme | `POST /api/urls` with `javascript:alert(1)` | `400` |
| Unknown code | `GET /api/urls/NOPE0000` | `404` |
| Bad range | analytics with `toUtc` before `fromUtc` | `400` |
| CORS | preflight from `http://localhost:4200` | `Access-Control-Allow-Origin: http://localhost:4200` |
| Dashboard | `GET http://localhost:4200/` | `200`, title "Shortform \| URL Intelligence" |
| Refresh (browser) | Click Refresh after a redirect | Totals update, and the selected link's chart shows the new click |
| Chart labels (browser) | Analytics for a link with a click | Bar labeled with the UTC day (for example "Oct 12") |

The dashboard calls the API at `http://localhost:5080` (see Limitations).

## 2. Architecture Overview

### 2.1 Components

```
Angular dashboard (frontend/url-shortener-ui)
        |  HTTP/JSON, CORS allow-list
        v
ASP.NET Core API (src/UrlShortener.Service)
  Controllers: UrlsController, RedirectController, HealthController
        |  DTOs (Contracts/), never EF entities
        v
UrlShortenerService (application layer, Services/)
        |  depends on abstractions
        v
Domain (src/UrlShortener.Domain)          Infrastructure (src/UrlShortener.Infrastructure)
  ShortUrl, ClickEvent entities             ShortUrlRepository, ClickEventRepository
  repository interfaces                     implement the domain interfaces
        |                                         |
        +-----------------+-----------------------+
                          v
Data (src/UrlShortener.Data)
  UrlShortenerDbContext, entity configurations, EF Core migrations
                          |
                          v
PostgreSQL 16 (Docker Compose) or any PostgreSQL reachable on the configured host
```

Dependencies point inward: the domain has no EF, ASP.NET, or Angular references. The layer rules come from `CONSTITUTION.md`.

### 2.2 Tools and Libraries

| Concern | Tool |
|---|---|
| UI | Angular 21, TypeScript, SCSS, Vitest through `ng test` |
| API | ASP.NET Core 8 controllers, SwaggerGen, ProblemDetails, built-in rate limiter |
| Data access | Entity Framework Core with the Npgsql provider; LINQ queries |
| Schema | EF Core Code First migrations (`InitialCreate`), reviewed SQL in `database/UrlShortener.sql` |
| Database | PostgreSQL 16 (`docker-compose.yml`) |
| Tests | xUnit, Testcontainers PostgreSQL, `WebApplicationFactory` |
| Config | `.env` loaded by DotNetEnv; `appsettings.json` for origins and public base URL |

### 2.3 Execution Approach

The prototype is synchronous and request-driven. There is no background worker, queue, or agent. Each request runs to completion inside one scoped DI container:

- Controllers validate HTTP shape and delegate to `UrlShortenerService`.
- The service applies the business rules, calls repositories, and maps results to response DTOs.
- Repositories use the scoped `DbContext`, with async EF Core calls and `CancellationToken`.
- `TimeProvider` is injected, so time-dependent rules (expiry, UTC timestamps) can be tested deterministically.

### 2.4 Control Flow

**Create (`POST /api/urls`)**

1. Rate limiter policy `create-url`: 30 requests per minute per remote IP. Excess requests get `429`.
2. Service validates the destination: absolute URI, `http` or `https` only, no embedded credentials. Failure is `InvalidDestinationUrlException` and returns `400` with ProblemDetails.
3. Service generates an 8-character code from a cryptographically random generator (`RandomNumberGenerator.GetString`) over a 57-character alphabet without look-alike characters.
4. Repository inserts the row. A unique violation on `IX_short_urls_code` becomes `ShortCodeConflictException`, and the service retries up to 3 attempts in total.
5. Response is `201 Created` with a `Location` header pointing to the details route.

**Redirect (`GET /{code}`)**

1. Lookup by code. Unknown code returns `404`.
2. `IsAvailableAt(now)` checks active state and expiry. Inactive or expired links return `404`.
3. A click event is written. If that write fails, the failure is logged as a warning and the redirect still succeeds.
4. Response is `302 Found` to the stored destination. The 302 is deliberate: a permanent redirect could be cached and bypass click tracking.

**Analytics (`GET /api/urls/{code}/analytics?fromUtc=&toUtc=`)**

1. Defaults to the last 30 days when no range is given. Rejects `start >= end` and ranges over 366 days with `400`.
2. Returns daily click counts for the range and the all-time total.

**Health (`GET /health`)**: calls `CanConnectAsync`. Returns `200` if the database answers, `503` otherwise.

### 2.5 Key Decisions

| Decision | Why | Trade-off |
|---|---|---|
| 302 redirects | Keeps every click reaching the server. | Slightly more load than a cacheable 301. |
| 8-character random codes with retry | Hard to guess, small enough to share, and collisions are caught by the database. | Codes are not sequential, so there is no ordering signal. |
| Click capture inside the redirect request, best-effort | Simple and no extra infrastructure. Redirects are not blocked by analytics failures (per `SPEC.md` section 7). | Clicks can be lost on database failure, and the redirect pays the write cost. A durable queue or outbox is needed for production durability. |
| Injected `TimeProvider` | Expiry and UTC rules are unit-testable without sleeping. | None significant. |
| DTOs, never EF entities, in responses | Keeps the API contract independent of the schema. | Some mapping code. |
| Code First migrations, SQL generated from them | Single source of truth for the schema. | Requires running `dotnet ef` after any model change. |
| Rate limit on create only | Protects the write path, where abuse costs the most. | Redirects and list calls are unthrottled. |
| No authentication | Matches the prototype scope. | Anyone who can reach the API can list all links and their analytics. |

## 3. Scenarios

Each scenario has five parts: the request, the decomposition (how the work was split), the execution (what was changed, in what order), the validation (what was checked and what the check proves), and the outcome.

Provenance matters, so each scenario states how it was produced:

- **Scenario 1 is reconstructed.** No build transcript exists. The decomposition and order are inferred from the repository's layout, the migration timestamp, and the code's dependencies. Treat its order as the most likely build sequence, not as observed history.
- **Scenario 2 was performed and observed** in this session, including one smaller brownfield fix (Section 2b below).
- **Scenario 3 was performed and observed.** It stopped at a clarifying question, and the user then restated the request.

### Scenario 1: Greenfield (building the service from `SPEC.md`)

**Request.** Build a URL shortener with an Angular dashboard, an ASP.NET Core 8 API, and PostgreSQL, following `SPEC.md`. Nothing exists yet.

**Decomposition.** The spec's section 3 layout and section 2 layering allow a bottom-up order, because each layer depends only on the layers inside it:

| Step | Work item | Depends on | Why this position |
|---|---|---|---|
| 1 | Domain entities: `ShortUrl`, `ClickEvent`, URL validation, expiry rule | none | Pure rules with no framework references; everything else depends on them |
| 2 | Repository interfaces in the domain | 1 | Inner layer owns the abstraction (`CONSTITUTION.md` Architecture 1) |
| 3 | EF Core `DbContext`, entity configurations (unique index on `short_urls.code`, composite index on `click_events`), `InitialCreate` migration | 1, 2 | Persistence maps the entities; schema comes from migrations (Data 2) |
| 4 | Repository implementations in Infrastructure | 2, 3 | Implements the domain interfaces against the `DbContext` |
| 5 | Application service: create with retry, get, list, analytics, redirect | 1, 2, 4 | Business flow; depends only on abstractions |
| 6 | Controllers and request/response DTOs (`Contracts/`) | 5 | Thin HTTP layer that delegates to the service |
| 7 | Angular dashboard calling the API | 6 | Consumes the stable contract |
| 8 | Unit and integration tests | 1 to 7 | Verifies each layer and the end-to-end path |

**Execution.** The repository's layout follows the same order: `src/UrlShortener.Domain/Entities` and `Repositories` first, then `src/UrlShortener.Data/Configurations` and `Migrations`, then `src/UrlShortener.Infrastructure/Repositories`, then `src/UrlShortener.Service/Services`, then `Controllers` and `Contracts`, then `frontend/url-shortener-ui`. The `InitialCreate` migration and the reviewed `database/UrlShortener.sql` came from the same work, which matches `CONSTITUTION.md` Data 2.

Two decisions made during this phase are visible in the code:

- Codes are generated with `RandomNumberGenerator`, not a sequence, so they cannot be guessed. Collisions are handled by catching the unique-index violation and retrying up to three attempts.
- Redirects return `302`, not `301`, so browsers keep sending clicks to the server.

**Validation.** Each acceptance area in `SPEC.md` section 9 maps to a check that exists today:

| Check | Evidence | What it proves |
|---|---|---|
| Unit: code-conflict retry | `ShortUrlTests.CreateAsync_RetriesWhenGeneratedCodeConflicts` | Collision handling works without a database |
| Unit: unsafe destinations rejected | `Create_RejectsUnsupportedOrUnsafeDestinations` (4 cases) | `javascript:`, `file:`, embedded credentials, and relative URLs are refused |
| Unit: UTC normalization | `Create_NormalizesDestinationAndTimestampsToUtc` | Timestamps are stored in UTC regardless of input offset |
| Unit: expiry boundary | `IsAvailableAt_ReturnsFalseAtExpiryBoundary` | A link is unavailable exactly at its expiry time |
| Integration: full flow | `CreateDetailsRedirectAndAnalytics_WorkTogether` against PostgreSQL | Create, details, `302` redirect, and analytics work together on a real schema |
| Integration: unsafe scheme | `Create_RejectsUnsupportedUrlScheme` | The API returns `400` for `javascript:` input |
| End-to-end run | Manual run of the live stack (Section 1) | The shipped configuration works, including CORS and the dashboard origin |

Results on 2026-10-12: 7 unit tests and 2 integration tests passed.

**Gaps found in validation.** Error responses are not fully consistent: unknown codes return `404` with an empty body, not ProblemDetails (`SPEC.md` sections 5 and 8). Analytics failure behavior, expired and disabled link responses, health, and rate limiting have no automated tests. These are tracked as T-006 to T-009 in `TASK.md`.

**Outcome.** A working service that meets the core functional requirements (`SPEC.md` section 4, items 1 to 7), with the gaps above.

### Scenario 2: Brownfield (restyling the existing dashboard)

**Request.** Make the existing Angular dashboard look like a premium executive dashboard: darker contrast, stronger KPI emphasis, modern table and chart styling, and subtle glassmorphism. The app already works, so the change must not break it.

**Decomposition.** Before editing, the existing system was read to find where each concern lives and what must stay fixed:

| Concern | Where it lives | Constraint |
|---|---|---|
| Rendered markup | `src/app/dashboard.html` | Its class names are the contract with the component and tests; keep them |
| Component styles | `src/app/app.scss` | Safe to rewrite |
| Global tokens and background | `src/styles.scss` | Safe to rewrite |
| Behavior | `src/app/app.ts` and `services/` | Must not change |
| Unused file | `src/app/app.html` | Starter placeholder, not rendered; left in place and tracked as T-015 |

Decisions:

- **Scope is styling only.** Markup and TypeScript stay unchanged, so behavior cannot regress through the restyle.
- **Dark theme with glass panels**, keeping the existing green accent and the Space Grotesk, DM Sans, and DM Mono fonts.
- **"Darker contrast" was read as a dark theme.** This is an interpretation, and it was stated in the final message so it could be corrected.

**Execution.**

1. `styles.scss`: replaced the light surface tokens with dark background, glass, text, and accent tokens (`--glass`, `--glass-border`, `--green`, and so on). Added a layered background gradient.
2. `app.scss`: rewrote the component styles over the same class names. The shared glass surface is one rule for panels, cards, and the table container. KPI cards became separate panels with a top hairline; the highlight card gets a green glow. Chart bars got gradient fills, horizontal gridlines, and a brighter latest-day bar. Table headers are sticky, rows have hover states, and the selected row has an accent edge. The responsive breakpoints from the original were kept and adjusted for the wider sidebar.
3. `app.scss`: bumped the smallest text sizes for readability, because the original sizes (8 to 11 px) were hard to read on the darker background.

**Validation.** Each check was run and recorded.

| Check | Result | What it proves |
|---|---|---|
| `ng build` | Succeeds | The SCSS compiles and the template still type-checks under `strictTemplates` |
| `ng test --watch=false` | 2 tests pass at the time | The existing Angular tests still ran (they are weak; see T-013). Five tests pass after the chart-label fix (Section 8). |
| Style budget | `app.scss` is 2.5 kB over its 12 kB budget | A build warning, not an error. Tracked as T-014 |
| Contrast of `--faint` on background | Raised from 3.6:1 to 5.4:1 | The change meets the 4.5:1 WCAG AA threshold for small text, which the original did not |
| Browser check | Not done | The look was checked by compilation and code review only, so layout and feel are unverified (T-016) |

**Second brownfield fix in the same session: `tsconfig` diagnostics.** A smaller change of the same kind. The editor reported an error in `tsconfig.app.json` and `tsconfig.spec.json`.

- *Decomposition:* read the diagnostic, then confirm whether the command-line compiler sees the same error.
- *Finding:* the command-line `tsc` (TypeScript 5.9 from `package.json`) passed. The editor's TypeScript requires `rootDir` whenever `outDir` is set. The error existed only in the editor.
- *Execution:* added `"rootDir": "./src"` to both files. The spec config uses `./src`, not `./src/app`, because it also includes `src/**/*.d.ts`.
- *Validation:* editor diagnostics are empty for both files, `tsc -p` exits 0 for both, `ng build` completes, and `ng test` passes.

Scenario 2 shows the brownfield pattern: read first, separate what must stay fixed from what can change, change the smallest surface that delivers the request, and then check the change with the same tools the project already uses.

### Scenario 3: Ambiguous (a deliverable list that did not match the code)

**Request.** "Document the deliverables for the prototype: a working prototype, architecture overview (components, tools, execution approach, control flow, key decisions), three scenarios (greenfield, brownfield, ambiguous, each with decomposition, execution, and validation), setup instructions, and testing approach with limitations and trade-offs."

**Decomposition.** Before writing anything, the request was mapped onto the repository. The list describes an agent-based prototype: task decomposition, tool execution, and validation. The repository contains none of that. Its contents are a URL shortener (`src/`, `frontend/`, `tests/`), and its docs (`README.md`, `SPEC.md`, `CONSTITUTION.md`) describe that application only. The request therefore had at least three readings:

1. The agent prototype lives in another repository or folder.
2. Build the agent prototype in this repository, then document it.
3. Document a planned agent system as a design, with no code.

**Execution.** Writing the document as requested would have meant describing a system that is not in the code, and presenting it as built. Rather than guess, the agent stopped and asked the user to choose among those readings.

The user interrupted the question and restated the request: document the deliverables for the current project. The reading that fit the repository was then applied. Each deliverable was mapped to something real in this codebase: the URL shortener as the prototype, its layers as the architecture, and the three scenarios as change-and-verify walkthroughs that are labeled by provenance (Section 3 intro).

**Validation.** The documented claims were checked against the code and the running system, not against the original list alone:

- Commands in Setup were run. The solution build, the unit, integration, and Angular test suites, and `ng build` all passed.
- The live stack was exercised: create, redirect, details, analytics, error cases, CORS, and the dashboard origin.
- Specific factual claims in the document were recomputed rather than trusted from memory. For example, the code alphabet size (57 characters, no look-alike characters), the contrast ratios, and the index definitions in `ClickEventConfiguration.cs` and `ShortUrlConfiguration.cs`.
- Two claims were corrected during this check: the contrast figure was 3.6:1, not 3.4:1, and the Angular dependency note was reworded to match the README.

**Lesson for the scenario.** A request that names a deliverable list can differ from the repository it targets. The cheap check is to map each named item to a file or command before writing, and to stop on the first item that has no counterpart.

## 4. Setup

### 4.1 Prerequisites

- .NET SDK 8 or later (verified with 10.0.401)
- Node.js with npm (`README.md` recommends `npm ci --legacy-peer-deps` for the Angular dependencies)
- Docker, if you use the Compose PostgreSQL service
- The EF Core CLI: `dotnet tool install --global dotnet-ef` (verified with 8.0.24)

### 4.2 Port 5432 conflict (read this first)

On the verification machine, a Windows PostgreSQL 18 service already listened on `localhost:5432`. The Docker container also publishes 5432. Whichever process binds first answers `localhost:5432`. In this run, the EF CLI and API used the host server, and `docker exec` inspected the container, so the two databases looked different.

Choose one:

- Stop the host PostgreSQL service and use the Compose container, or
- Change the Compose host port (for example `5433:5432`) and set `POSTGRES_PORT=5433` in `.env`.

After each change, confirm the target with `dotnet ef dbcontext info` (it prints the data source) before running migrations.

### 4.3 Steps

1. Copy the environment template and set a local password:
   ```powershell
   copy .env.example .env
   ```
   Edit `.env` and set `POSTGRES_PASSWORD`. Do not commit `.env`.

2. Start PostgreSQL (see 4.2 for the port check):
   ```powershell
   docker compose up -d postgres
   ```

3. Apply migrations:
   ```powershell
   dotnet ef database update --project src/UrlShortener.Data/UrlShortener.Data.csproj --startup-project src/UrlShortener.Service/UrlShortener.Service.csproj
   ```
   If this fails with "file is locked", a running API process holds the build output. Stop it and retry.

4. Start the API (http://localhost:5080, Swagger at `/swagger`):
   ```powershell
   dotnet run --project src/UrlShortener.Service/UrlShortener.Service.csproj --launch-profile http
   ```

5. Start the dashboard (http://localhost:4200):
   ```powershell
   cd frontend/url-shortener-ui
   npm ci --legacy-peer-deps
   npm start
   ```

6. Check: `curl http://localhost:5080/health` should return `{"status":"healthy"}`.

Regenerating the reviewed SQL after a model change is covered in `README.md`.

## 5. Testing Approach

### 5.1 Suites

| Suite | Command | Scope | Result (2026-10-12) |
|---|---|---|---|
| Unit (xUnit) | `dotnet test tests/UrlShortener.UnitTests/UrlShortener.UnitTests.csproj` | Domain rules, service retry logic, UTC normalization, expiry | 7 passed |
| Integration (xUnit) | `dotnet test tests/UrlShortener.IntegrationTests/UrlShortener.IntegrationTests.csproj` | Full API against PostgreSQL in a Testcontainers container; requires Docker | 2 passed (about 30 s) |
| Frontend (`ng test`) | `cd frontend/url-shortener-ui; npx ng test --watch=false` | Angular unit tests: 2 starter tests and 3 chart-label tests | 5 passed |
| Build | `dotnet build UrlShortener.slnx`; `npx ng build` | Compilation, style budgets | Succeeded; `ng build` warns about `app.scss` budget |

The `README.md` test commands use `npm test -- --watch=false`. Both forms run the same Angular suite.

### 5.2 What Is Covered

- Destination validation: rejected schemes, embedded credentials, relative URLs.
- Code-conflict retry.
- Expiry boundary (`IsAvailableAt`).
- UTC normalization.
- End-to-end create, details, redirect, analytics, and scheme rejection against real PostgreSQL.
- Chart date labels are formatted in UTC, including in a time zone east of UTC. The regression test was checked by reintroducing the old behavior: it failed with "Oct 11" instead of "Oct 12".

### 5.3 What Is Not Covered by Automated Tests

- Unknown-code `404` on details and redirect.
- Expired or inactive link redirects.
- Analytics range validation (the `400` cases).
- Health endpoint, both healthy and unhealthy.
- Rate limiting (`429`).
- Click-recording failure while the redirect still succeeds.
- Database failure behavior in the service.
- Dashboard behavior and layout. Component tests are still limited to the two starter tests. The refresh fix was checked in a browser run, not by a unit test.

These were checked by hand in Section 1, not by tests.

### 5.4 Testing Approach Going Forward

`CONSTITUTION.md` requires unit tests for application logic and integration tests for API, EF Core, and PostgreSQL behavior. Add tests for each gap in 5.3, starting with the redirect and analytics paths, because they carry the most behavior.

## 6. Limitations

- **Port 5432 conflict** with a host PostgreSQL service (Section 4.2).
- **Hardcoded API URL.** `url-api.service.ts` uses `http://localhost:5080/api/urls`. `SPEC.md` section 9 requires the dashboard to call the configured API origin.
- **Empty 404 bodies.** Unknown codes return `404` with no ProblemDetails body, which differs from the error format used for `400` responses.
- **Analytics totals mismatch.** The dashboard places the all-time click total next to a 30-day chart. The "total clicks" number does not reflect the selected range. This is open (T-011, pending decision D1). The refresh and chart-date issues found in the same walkthrough are fixed (Section 8).
- **Best-effort click capture.** A database failure during redirect loses that click. Durable capture needs a queue or outbox.
- **Synchronous write on redirect.** Every redirect performs an insert. This adds latency and load on the hottest path.
- **No authentication or authorization.** The list endpoint returns the first 100 links and their click counts to any caller. `SPEC.md` requires authorization once user accounts exist.
- **No paging.** The list is capped at 100 with no way to page through more.
- **Expiry has no API.** The domain supports expiry, but the create request cannot set it.
- **Rate limiting uses the remote IP only.** Behind a reverse proxy, every client may share one address unless forwarded headers are configured.
- **Destination URLs are not inspected.** The redirect service does not fetch destinations, so SSRF is not a current risk. `SPEC.md` requires SSRF mitigation before any URL-inspection feature.
- **Style budget warning.** `app.scss` exceeds its 12 kB budget.
- **Unused placeholder.** `src/app/app.html` is an Angular starter file that is not rendered.
- **Visual dashboard checks** have not been automated.

## 7. Trade-offs Summary

- **Simplicity over durability** for analytics: fewer moving parts, at the cost of losing clicks on database failure.
- **Synchronous over asynchronous** for redirects: easier to reason about and test, but the redirect path is coupled to the database.
- **302 over 301** for correct click counts, at a small performance cost.
- **Code First over database-first** for schema control, which means every model change needs a migration and regenerated SQL.
- **Random codes over sequential IDs** for privacy and unguessability, which gives up ordering and simple collision-free generation.

## 8. Change Log

### 2026-10-12: Refresh and chart-date fixes

Found while running the project flow end to end (see `URLShortener-Project-Flow.docx`).

- **Refresh now reloads the selected link's analytics.** Before, the totals updated but the selected link's chart kept its old data. The reload now refreshes the selected link and its analytics. Responses for a link that is no longer selected are ignored, so a slow earlier response cannot overwrite the current chart. Changed in `src/app/app.ts`.
- **Chart date labels show the correct UTC day.** Before, a click on 2026-10-12 was labeled "Oct 11" for users east of UTC. The label is now built by `utcDayLabel` in `src/app/chart-labels.ts`, and the template uses it. Covered by `src/app/chart-labels.spec.ts`.
- **Verification:** `ng test` passes (5 tests). `tsc` passes for both configs, `ng build` succeeds (only the existing `app.scss` budget warning, T-014), and the live app was checked in a browser. After Refresh, the selected link's chart shows the redirect's click, and the bar reads "Oct 12".
- **Documents updated:** this file, `TASK.md`, `PLAN.md`, `README.md`, the frontend README, and the project-flow Word document.
