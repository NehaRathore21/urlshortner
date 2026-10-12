# URLShortener Task List

Written for: engineers picking up work. Work the tasks in ID order unless the Depends column says otherwise. Each task is complete only when its acceptance check passes and its tests are added or updated (`CONSTITUTION.md` Verification 1).

Plan and rationale: `PLAN.md`. Requirements: `SPEC.md`. Status values: Not started, In progress, Done, Blocked.

Baseline (2026-10-12, after the refresh and chart-date fixes): all suites pass; 7 unit, 2 integration, 5 Angular tests (2 starter tests and 3 chart-label tests).

## Phase 0: Security and Hygiene

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-001 | Replace the password value in `.env.example` with a placeholder such as `<set-a-local-password>`. Confirm `.gitignore` still lists `.env`. | Config | none | `grep` for the current value across the repo finds nothing outside the ignored `.env`. Owner rotates the password if it was used elsewhere. | Not started |

## Phase 1: Domain

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-002 | Add `Task<long> GetRangeTotalCountAsync(long shortUrlId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken)` to `IClickEventRepository`. Confirm decision D1 in `PLAN.md` first. | Domain | D1 confirmed | Interface compiles; all implementations are updated in the same change. | Not started |

## Phase 2: Data and Infrastructure

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-003 | Implement the range count in `ClickEventRepository` using LINQ, with try/catch and logging like the other methods. Keep `from` inclusive and `to` exclusive. | Infrastructure | T-002 | Method returns the same count the daily aggregate would return for the range. | Not started |
| T-004 | Integration test: range count against real PostgreSQL, covering inclusive `from`, exclusive `to`, and an empty range. | Tests (integration) | T-003 | Test passes in `dotnet test`; boundary cases are asserted explicitly. | Not started |

## Phase 3: Service

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-005 | Change `UrlShortenerService.GetAnalyticsAsync` so `TotalClicks` is the range total. Daily counts are unchanged. | Service | T-003 | Analytics for a range returns that range's total. Existing contract fields are preserved. | Not started |
| T-006 | Unit tests with fake repositories for: expired link returns null, disabled link returns null, click-recording failure still returns the destination, analytics repository failure propagates. | Tests (unit) | none | All four cases pass. Click-recording failure is logged, not thrown. | Not started |

## Phase 4: API

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-007 | Return ProblemDetails with `404` (title `Short URL not found.`, per decision D2) from `UrlsController.GetByCode`, `GetAnalytics`, and `RedirectController`. | API | D2 confirmed | Response body is `application/problem+json` on all three routes. `ProducesResponseType` is updated to match. | Not started |
| T-008 | Integration tests for: `404` problem bodies, invalid analytics range (`400`), `GET /health` healthy response, and the redirect `302` with `Location`. | Tests (integration) | T-007 | All cases pass; each asserts the status code and, where applicable, the content type. | Not started |
| T-009 | Integration test for the create rate limit: 31st request within a minute returns `429`. Put it in its own test class because it is slow. | Tests (integration) | none | Test passes reliably in a clean database. | Not started |

## Phase 5: Frontend

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-010 | Move the API base URL into Angular environment files (`src/environments/environment.ts` and `environment.production.ts`) and wire them through `fileReplacements` in `angular.json`. `UrlApiService` reads from the environment. | Frontend | none | No hardcoded `localhost:5080` remains in `src/`. `ng build` and `ng build --configuration production` both succeed. Satisfies `SPEC.md` §9. | Not started |
| T-011 | Make the analytics total match the selected range. If D1 is confirmed, relabel to show the range total. If D1 is rejected, label it "All time". | Frontend | T-005, D1 | The label next to the 30-day chart describes the number shown. | Not started |
| T-012 | Replace the Details action's behavior (currently it only selects the link for analytics) with a details panel showing created time, expiry, active state, and click count. | Frontend | T-010 | Panel shows all four fields for the selected link. Keyboard and screen-reader labels are present. | Not started |
| T-013 | Replace `src/app/app.spec.ts` (which mocks prototype methods) with TestBed tests: default tab, tab switching, create-link validation message, and the error notice when the API is unreachable. | Tests (frontend) | T-010 | Tests render the component and assert on the DOM. `npm test -- --watch=false` passes. | Not started |
| T-014 | Clear the `app.scss` style-budget warning by trimming the stylesheet. Only raise `angular.json`'s budget if trimming cannot reach the limit, and state that choice in the change. | Frontend | none | `npx ng build` completes with no budget warning. | Not started |
| T-015 | Delete `src/app/app.html`, the unused Angular starter template. | Frontend | none | No references remain; build and tests pass. | Not started |
| T-016 | Check the dashboard in a browser at desktop width and at 700 px. Confirm the table, chart, and KPI cards have no horizontal page scroll outside the table container. | Frontend | T-014 | Screenshots or notes recorded in the change. Any layout issue becomes a new task. | Not started |

## Phase 6: Verification

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-017 | Run the full verification: `dotnet build UrlShortener.slnx`, `dotnet test UrlShortener.slnx`, `npm test -- --watch=false`, `npx ng build`. Walk the end-to-end steps in `docs/PROTOTYPE.md` §1 against the running stack. | Verification | T-001 through T-016 | Every command passes, with no budget warning. Every end-to-end step matches its expected result. | Not started |

## Phase 7: Documentation

| ID | Task | Layer | Depends | Acceptance | Status |
|---|---|---|---|---|---|
| T-018 | Add two items to `README.md` setup: the port 5432 conflict with a host PostgreSQL service (see `docs/PROTOTYPE.md` §4.2), and the `dotnet-ef` install prerequisite. | Docs | none | A new developer can follow `README.md` without hitting the port conflict unnoticed. | Done (2026-10-12) |
| T-019 | Replace the Angular CLI boilerplate in `frontend/url-shortener-ui/README.md` with project setup, the API origin configuration from T-010, and the test commands. Remove the `ng e2e` section, because no e2e framework is configured. | Docs | T-010 | README describes only commands that work in this repo. | Done (2026-10-12). Describes the hardcoded API origin until T-010 is done. |
| T-020 | Update `docs/PROTOTYPE.md`: remove closed gaps from §3 and §6, and update §5 test counts. | Docs | T-017 | Document matches the verified state after T-017. | In progress. Test counts and the change log are updated; re-check after T-017. |

## Completed Work

| ID | Work | Evidence | Status |
|---|---|---|---|
| D-001 | Dashboard restyle: dark glass theme, KPI cards, chart and table styling | `ng build` and `ng test` pass. Visual check pending in T-016. | Done |
| D-002 | Prototype documentation in `docs/PROTOTYPE.md` | File exists; gaps tracked in T-020 | Done |
| D-003 | End-to-end run of the API and dashboard | Health, create, redirect, details, analytics, validation, 404, range, CORS, and dashboard served, all verified 2026-10-12 | Done |
| D-004 | Refresh reloads the selected link's analytics (walkthrough issue I-1) | `app.ts` `reload()` and `loadAnalytics()`. Browser run: after Refresh, the chart total reads 1 and the bar is shown. Stale responses for an unselected link are ignored. | Done (2026-10-12) |
| D-005 | Chart date labels show the UTC day (walkthrough issue I-2) | `src/app/chart-labels.ts`, used by `dashboard.html`. Regression test `chart-labels.spec.ts` fails with "Oct 11" when the old formula is restored, and passes with the fix. Browser run shows "Oct 12". | Done (2026-10-12) |
| D-006 | Project documents updated, including the flow Word document | `docs/PROTOTYPE.md` §8 change log; `README.md`; frontend README; `docs/URLShortener-Project-Flow.docx` regenerated from new captures | Done (2026-10-12) |

## Backlog (not scheduled)

These items are in `PLAN.md` §7. Each needs an owner decision before it becomes a task.

| ID | Item |
|---|---|
| B-001 | Authentication and authorization |
| B-002 | Paging for the links list |
| B-003 | Durable click capture (queue or outbox) |
| B-004 | Proxy-aware rate limiting |
| B-005 | Expiry on link creation |
| B-006 | SSRF mitigation before any URL inspection |
