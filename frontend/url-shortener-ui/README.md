# URLShortener dashboard (Angular)

The Angular dashboard for the URLShortener. It creates short links, lists them, and shows click analytics from the ASP.NET Core API. Built with Angular CLI 21.2.26.

For the whole system, see the repository root [README.md](../../README.md) and [docs/PROTOTYPE.md](../../docs/PROTOTYPE.md).

## Setup

Install dependencies (the lockfile needs the legacy peer-dependency flag):

```bash
npm ci --legacy-peer-deps
```

Start the API first (see the root README), then start the dashboard:

```bash
npm start
```

Open `http://localhost:4200/`. The dashboard reloads when you change a source file.

## API address

The dashboard calls `http://localhost:5080/api/urls`. This address is hardcoded in `src/app/services/url-api.service.ts`. Moving it into Angular environment configuration is task T-010 in `docs/TASK.md`. Until then, change that one constant to point at another API.

## Commands

| Task | Command |
|---|---|
| Start the dev server | `npm start` |
| Run the unit tests (Vitest, through Angular CLI) | `npm test -- --watch=false` |
| Type-check the app and spec configs | `npx tsc -p tsconfig.app.json --noEmit` and `npx tsc -p tsconfig.spec.json --noEmit` |
| Production build (output in `dist/`) | `npx ng build` |

`npx ng build` currently reports that `src/app/app.scss` is over its 12 kB style budget. This is a warning, and it is tracked as T-014.

No end-to-end framework is configured. `ng e2e` is not available in this project.

## Source layout

| Path | Purpose |
|---|---|
| `src/app/app.ts` | Dashboard component: loads links, creates links, selects a link, and loads its analytics |
| `src/app/dashboard.html` | The dashboard template |
| `src/app/app.scss` | Component styles |
| `src/styles.scss` | Global design tokens and the page background |
| `src/app/services/url-api.service.ts` | HTTP client for the API |
| `src/app/services/url-types.ts` | Types for the API responses |
| `src/app/chart-labels.ts` | Formats chart dates as UTC days |
| `src/app/app.html` | Unused Angular starter template, scheduled for removal (T-015) |
