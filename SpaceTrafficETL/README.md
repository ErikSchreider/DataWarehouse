# SpaceTrafficETL

Production-style .NET 10 Worker Service for downloading space datasets, storing raw historical files, parsing CelesTrak TLE/GP data, and preparing staging CSV files for Exasol import.

## Run locally

```powershell
dotnet run --project SpaceTrafficETL.csproj
```

## Docker

```powershell
docker build -t spacetrafficetl .
docker run --rm -v ${PWD}/data:/app/data spacetrafficetl
```

## Output layout

```text
data/
  raw/{source}/{yyyy}/{MM}/{dd}/{timestamp}_{source}{extension}
  staging/{yyyy}/{MM}/{dd}/satellite_tle_stage.csv
```

The staging CSV is designed for an Exasol import step. Database connectivity and migrations are intentionally not implemented.
