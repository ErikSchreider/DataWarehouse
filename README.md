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
docker compose --env-file .env up -d --build
```

Legacy challenge and diagram files are stored in `archived-challenge-files/`.

## Server Deployment

Prerequisites on the server:

- Docker and Docker Compose.
- A Linux x86-64 Docker host. Exasol Docker DB requires privileged mode.
- The Exasol ODBC driver installed in the runtime image or mounted into the container and registered in `odbcinst.ini`.
- A database user with rights to create/use the configured schema for ETL. The web user can be read-only after the schema is loaded.

Create a local `.env` file from the template and change the values:

```powershell
Copy-Item .env.example .env
```

Start ETL and web:

```powershell
docker compose --env-file .env up -d --build
```

The web app is available on `http://<server>:8085` by default. The analytics page is `/analytics`.

Important: the Dockerfiles install `unixodbc`, but the proprietary Exasol ODBC driver must still be provided for the driver name configured by `EXASOL_ODBC_DRIVER`.

## Portainer Stack

In Portainer create a new stack from this repository and use `docker-compose.yml`.

Add these environment variables in the Portainer stack editor:

```text
EXASOL_DOCKER_TAG=2025.1.11
EXASOL_EXTERNAL_PORT=9563
EXASOL_HOST=exasoldb
EXASOL_PORT=8563
EXASOL_SCHEMA=SPACE_TRAFFIC
EXASOL_USER=sys
EXASOL_PASSWORD=exasol
EXASOL_ODBC_DRIVER=EXASOL
ETL_INTERVAL_HOURS=24
UCS_DOWNLOAD_URL=<direct-csv-or-xlsx-download-url>
UCS_RAW_FILE_EXTENSION=.csv
SPACEDEVS_LAUNCH_URL=https://ll.thespacedevs.com/2.2.0/launch/
ETL_DATA_DIR=/opt/space-traffic-etl/data
WEB_PORT=8085
WEB_KEYS_DIR=/opt/space-traffic-web/keys
```

If Exasol runs as another container in Portainer, put both stacks on a shared Docker network and use the Exasol container or service name as `EXASOL_HOST`. If Exasol runs outside Docker, use the server IP or DNS name and make sure port `8563` is reachable from inside the containers.

The default stack starts its own Exasol container named `exasoldb` from `exasol/docker-db`. It exposes Exasol on host port `9563` and persists database files in the named Docker volume `exasol-data`. Inside the stack, ETL and web connect to `exasoldb:8563`.

The ETL uses these data sources by default:

- CelesTrak GP JSON: `active`, `debris`, `starlink`, `geo`, `stations`, `last-30-days`.
- UCS Satellite Database: direct CSV/TSV/XLSX file configured through `UCS_DOWNLOAD_URL`.
- SpaceDevs Launch Library API: `https://ll.thespacedevs.com/2.2.0/launch/`.

For UCS, the public resource page is not always the direct table download. If the ETL log says the UCS dataset appears to be HTML, set `UCS_DOWNLOAD_URL` to the real CSV/XLSX download link and set `UCS_RAW_FILE_EXTENSION` to `.csv`, `.tsv`, or `.xlsx`.

If you already created the old schema in Exasol, recreate the `SPACE_TRAFFIC` schema or manually add the new staging/fact columns before running the updated ETL. The bootstrap scripts skip existing tables and do not alter old table definitions automatically.
