FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY SpaceTrafficETL.csproj ./
RUN dotnet restore SpaceTrafficETL.csproj

FROM restore AS publish
COPY . ./
RUN dotnet publish SpaceTrafficETL.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false
RUN mkdir -p /app/publish/data

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS final
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ODBCSYSINI=/etc \
    ODBCINI=/etc/odbc.ini

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates unixodbc \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/data \
    && chown -R $APP_UID:0 /app

COPY --from=publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "SpaceTrafficETL.dll"]
