# URLShortener Delivery Plan

Written for: the project owner and engineers working on the URLShortener, to decide what to build next and in what order.

Baseline: verified on 2026-10-12. Requirements are in `SPEC.md`, rules are in `CONSTITUTION.md`, and the ordered work items are in `TASK.md`.

## 1. Purpose and Scope

This plan closes the gaps between the current prototype and the acceptance criteria in `SPEC.md` section 9. It covers the existing features only: create, details, redirect, analytics, list, and the Angular dashboard. Features that are not in the spec are listed as backlog and are not scheduled.

## 2. Current State

| Area | State |
|---|---|
| Solution build | Passes (`dotnet build UrlShortener.slnx`, 0 warnings, 0 errors) |
| Unit tests | 7 passing |
| Integration tests | 2 passing (Testcontainers PostgreSQL) |
| Frontend tests | 5 passing (`ng test`): 2 starter tests, which are weak (see T-013), and 3 chart-label tests |
| Frontend build | Passes, with a style-budget warning (T-014) |
| End-to-end run | Create, redirect, details, analytics, validation, and CORS verified against the running stack |
| Schema | EF Core migration `InitialCreate` matches `database/UrlShortener.sql` |

Known gaps against `SPEC.md`:

1. Error responses are inconsistent. `404` responses return empty bodies, not ProblemDetails (SPEC §5, §8).
2. Analytics `totalClicks` is all-time, but the endpoint is range-scoped and the dashboard shows it beside a 30-day chart (SPEC §5).
3. The dashboard hardcodes `http://localhost:5080`, not a configured origin (SPEC §9).
4. Test coverage misses several behaviors the spec requires (SPEC §9).
5. `.env.example` contains a value that looks like a real password. This is a security issue, not only a documentation issue.
6. The dashboard's "Details" action only selects a link for analytics; it does not show expiry or active state (SPEC §4 item 7).

Resolved on 2026-10-12, found during the project-flow walkthrough (not in the original list):

- Refresh did not reload the selected link's analytics, so the chart could show "no clicks" after a visit. Fixed in `app.ts` (D-004).
- Chart date labels were one day early for users east of UTC. Fixed with `utcDayLabel` and a regression test (D-005).

## 3. Design Rules for This Plan

These come from `SPEC.md` and `CONSTITUTION.md`. Every task in `TASK.md` follows them.

- **Layer order.** Work proceeds inward-out: Domain, then Data, then Infrastructure, then Service, then API, then Frontend, then verification and docs. Dependencies point inward, so a layer is never changed to fit a layer above it.
- **Tests travel with behavior.** Each behavior change includes xUnit tests (unit for logic, integration for API, EF Core, or PostgreSQL behavior) before the task is complete (`CONSTITUTION.md` Verification 1).
- **Contracts stay DTO-based.** Responses use `Contracts/` DTOs, never EF entities (`CONSTITUTION.md` Data 1).
- **Errors use ProblemDetails.** API errors follow one format and never leak internals (`SPEC.md` §8).
- **Times are UTC.** `DateTimeOffset` at API boundaries, and UTC for every stored or compared time.
- **Configuration is not hardcoded.** Origins and API addresses come from configuration (`SPEC.md` §9).
- **Migrations are the schema source of truth.** Any schema change updates the migration and `database/UrlShortener.sql` together.

## 4. Phases

Phases run in order. Within a phase, tasks with no dependency on each other can run in parallel.

### Phase 0: Security and Hygiene

Exit criterion: no credential-like values are present in any committed file.

- Replace the example password in `.env.example` with a placeholder (T-001).
- If the password was used anywhere outside local development, rotate it. This needs the owner.

This phase goes first because it removes exposure that does not depend on any other work.

### Phase 1: Domain

Exit criterion: the analytics totals decision (D1) is recorded.

- No entity changes are planned. `ShortUrl` and `ClickEvent` already enforce the rules the spec requires.
- Add the range-total method to the `IClickEventRepository` interface (T-002). This is a domain contract change, so it comes before the data layer.

### Phase 2: Data and Infrastructure

Exit criterion: range-scoped click counts are implemented and tested against real PostgreSQL.

- Implement the range-count query in `ClickEventRepository` with try/catch and logging, following the existing pattern (T-003).
- Add integration tests for the half-open range boundaries (T-004).
- Update `database/UrlShortener.sql` only if a raw SQL query is added. The plan prefers LINQ, so the script should not change.

### Phase 3: Service

Exit criterion: service behavior matches the spec for analytics, redirect, and failure paths, with unit tests.

- Change `GetAnalyticsAsync` to return the range total (T-005).
- Add unit tests for expired and disabled redirects, click-recording failure, and analytics failure propagation (T-006).

### Phase 4: API

Exit criterion: every error response uses ProblemDetails, and the API is covered by integration tests for the error and limit cases.

- Return ProblemDetails for `404` on details, analytics, and redirect (T-007).
- Add integration tests for error bodies, invalid analytics ranges, health, and create rate limiting (T-008, T-009).

### Phase 5: Frontend

Exit criterion: the dashboard reads its API origin from configuration, shows analytics that match the selected range, and passes real component tests.

- Move the API base URL into Angular environment configuration (T-010).
- Relabel the analytics total to match the range, or confirm it once D1 is applied (T-011).
- Add a details panel for the selected link (T-012).
- Replace the mock-based spec with TestBed tests (T-013).
- Fix the style-budget warning and remove the unused starter template (T-014, T-015).
- Verify the dashboard visually in a browser at desktop and phone widths (T-016).

### Phase 6: Verification

Exit criterion: all suites pass together, and the SPEC §9 acceptance checklist is met.

- Run the full build and test suite (T-017).

### Phase 7: Documentation

Exit criterion: setup instructions match what was verified, and `docs/PROTOTYPE.md` describes the system as built.

- Add the port 5432 conflict and the `dotnet-ef` prerequisite to `README.md` (T-018, done).
- Replace the Angular CLI boilerplate in the frontend README (T-019, done).
- Update `docs/PROTOTYPE.md` to remove closed gaps (T-020, in progress until T-017 is done).

`SPEC.md` and `CONSTITUTION.md` are not edited by this plan. Changing them needs the owner's approval.

## 5. Decisions

| ID | Decision | Recommendation | Status |
|---|---|---|---|
| D1 | What `totalClicks` means in the analytics response | Total clicks within the requested range. The endpoint is range-scoped, and `SPEC.md` §5 asks for "click totals" for that range. | Proposed, awaiting owner confirmation |
| D2 | ProblemDetails body for `404` | Use `title: "Short URL not found."` and the standard ProblemDetails shape, matching the existing `400` responses. | Proposed |
| D3 | Click capture durability | Keep best-effort capture for this prototype, documented in `docs/PROTOTYPE.md`. Durable capture is backlog B-003. | Accepted |
| D4 | Visual theme | Dark glass theme, as delivered in the dashboard restyle. | Accepted |

If D1 is rejected in favor of all-time totals, T-011 changes to keep the all-time number and label it "All time" next to the chart.

## 6. Risks

- **Analytics semantics change (D1).** Clients that read `totalClicks` as all-time will see different numbers. The dashboard is the only client today, so the risk is low, but any external consumer must be checked first.
- **Port conflict on setup.** Developers may point the API at the wrong PostgreSQL instance and see empty data (`docs/PROTOTYPE.md` §4.2). The README note in T-018 reduces this risk.
- **Rate-limit test duration.** The 30-per-minute limit makes the integration test slower. Keep it in its own test class.
- **Visual regressions.** The restyle was verified by build and code review only. T-016 closes this gap.

## 7. Out of Scope and Backlog

These items are real needs but are not in the current spec or are conditional on a future decision. They are not scheduled.

| ID | Item | Trigger |
|---|---|---|
| B-001 | Authentication and authorization for management and analytics | User accounts are introduced (`SPEC.md` §7) |
| B-002 | Paging for the links list (currently capped at 100) | Link counts exceed what one page shows |
| B-003 | Durable click capture through a queue or outbox | Production durability is required (`SPEC.md` §7) |
| B-004 | Proxy-aware rate limiting using forwarded headers | Deployment sits behind a reverse proxy |
| B-005 | Setting an expiry when creating a link | Expiry is needed by users; the domain already supports it |
| B-006 | SSRF mitigation for any URL inspection feature | URL inspection is added (`SPEC.md` §7) |

## 8. Completion Criteria

The plan is complete when:

1. Every task in `TASK.md` marked Phase 0 through Phase 7 is Done.
2. `dotnet test UrlShortener.slnx` and `npm test -- --watch=false` pass.
3. `npx ng build` passes with no budget warning.
4. The `SPEC.md` §9 acceptance checklist is satisfied: the solution builds, tests pass, Swagger describes the API, and the dashboard calls the configured API origin.
5. `docs/PROTOTYPE.md` and `README.md` describe the system as built.
