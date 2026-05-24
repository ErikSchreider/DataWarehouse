# SpaceTraffic

.NET 10 solution with two projects:

- `src/SpaceTrafficETL`: Worker service for downloading, transforming, exporting, and loading space traffic data into Exasol.
- `src/SpaceTrafficWeb`: Read-only Blazor Server dashboard for Exasol analysis results.

## Build

```powershell
dotnet build SpaceTraffic.sln
```

## Run Web App

```powershell
dotnet run --project src\SpaceTrafficWeb\SpaceTrafficWeb.csproj
```

## Docker Compose

```powershell
docker compose -f docker-compose.etl.yml up -d
docker compose -f docker-compose.web.yml up -d
```

Legacy challenge and diagram files are stored in `archived-challenge-files/`.
