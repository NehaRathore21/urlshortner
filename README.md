# URLShortener

Angular dashboard and ASP.NET Core 8 API for creating short links and viewing click analytics. See [docs/SPEC.md](docs/SPEC.md) for behavior and architecture, [docs/CONSTITUTION.md](docs/CONSTITUTION.md) for project rules, [docs/PROTOTYPE.md](docs/PROTOTYPE.md) for the architecture and the verified flow, and [docs/TASK.md](docs/TASK.md) for open work.

## Prerequisites

- .NET SDK 8 or later (verified with 10.0.401)
- The EF Core CLI: `dotnet tool install --global dotnet-ef` (verified with 8.0.24)
- Node.js with npm
- Docker, to run PostgreSQL with Compose

## Local Setup

1. Copy `.env.example` to `.env` and set your own local PostgreSQL password in `.env`. Do not use the value shown in `.env.example`. The root `.env` is ignored by Git; do not commit secrets or access tokens.
2. Start PostgreSQL: `docker compose up -d postgres`.

   **Port 5432 conflict:** if PostgreSQL is already installed on this machine as a Windows service, it can answer on `localhost:5432` instead of the container, and the API will see a different, possibly empty, database. Either stop that service, or change the Compose port mapping (for example `5433:5432`) and set `POSTGRES_PORT=5433` in `.env`. Check the target before you migrate with `dotnet ef dbcontext info`, which prints the data source.
3. Apply the EF Core Code First migrations: `dotnet ef database update --project src/UrlShortener.Data/UrlShortener.Data.csproj --startup-project src/UrlShortener.Service/UrlShortener.Service.csproj`.
4. Start the API: `dotnet run --project src/UrlShortener.Service/UrlShortener.Service.csproj --launch-profile http`. If it fails with "file is locked", another API process is still running. Stop it and try again.
5. In another terminal, start Angular: `cd frontend/url-shortener-ui; npm ci --legacy-peer-deps; npm start`.

The Angular dashboard is at `http://localhost:4200`; API and Swagger are at `http://localhost:5080` and `http://localhost:5080/swagger`.

`database/UrlShortener.sql` is generated from the EF migration and includes prepared PostgreSQL operational queries. Regenerate the schema section after a model change with:

```powershell
dotnet ef migrations add <MigrationName> --project src/UrlShortener.Data/UrlShortener.Data.csproj --startup-project src/UrlShortener.Service/UrlShortener.Service.csproj --output-dir Migrations
dotnet ef migrations script --project src/UrlShortener.Data/UrlShortener.Data.csproj --startup-project src/UrlShortener.Service/UrlShortener.Service.csproj --output database/UrlShortener.sql
```

## Tests

```powershell
dotnet test UrlShortener.slnx
cd frontend/url-shortener-ui
npm test -- --watch=false
```

The integration tests use Testcontainers and require Docker to be running. The current Angular lockfile can be installed with `npm ci --legacy-peer-deps` if a clean install is needed.
