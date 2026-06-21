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
ARG EXASOL_ODBC_DRIVER_URL=""
ARG EXASOL_ODBC_DOWNLOAD_PAGE="https://downloads.exasol.com/clients-and-drivers/odbc"

ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ODBCSYSINI=/etc \
    ODBCINI=/etc/odbc.ini

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl tar unzip unixodbc \
    && rm -rf /var/lib/apt/lists/*

RUN if [ -n "$EXASOL_ODBC_DRIVER_URL" ] || [ -n "$EXASOL_ODBC_DOWNLOAD_PAGE" ]; then \
        mkdir -p /opt/exasol/odbc /tmp/exasol-odbc \
        && driver_url="$EXASOL_ODBC_DRIVER_URL" \
        && if [ -z "$driver_url" ]; then \
            curl -fsSL "$EXASOL_ODBC_DOWNLOAD_PAGE" -o /tmp/exasol-odbc-page.html \
            && driver_url="$(grep -Eoi 'https?://[^\"'\'' ]+' /tmp/exasol-odbc-page.html | grep -Ei 'linux|x86_64|amd64' | grep -Ei 'zip|tar|tgz|gz' | head -n 1)"; \
        fi \
        && test -n "$driver_url" \
        && curl -fsSL "$driver_url" -o /tmp/exasol-odbc-driver \
        && if echo "$driver_url" | grep -Eiq '\.zip($|\?)'; then \
            unzip -q /tmp/exasol-odbc-driver -d /tmp/exasol-odbc; \
        else \
            tar -xf /tmp/exasol-odbc-driver -C /tmp/exasol-odbc; \
        fi \
        && find /tmp/exasol-odbc -type f -name '*.so*' -exec cp {} /opt/exasol/odbc/ \; \
        && driver_path="$(find /opt/exasol/odbc -type f -name '*.so*' | grep -i odbc | head -n 1)" \
        && test -n "$driver_path" \
        && printf '[EXASOL]\nDescription=Exasol ODBC Driver\nDriver=%s\nUsageCount=1\n' "$driver_path" > /etc/odbcinst.ini \
        && rm -rf /tmp/exasol-odbc /tmp/exasol-odbc-driver; \
    fi

RUN mkdir -p /app/data \
    && chown -R $APP_UID:0 /app

COPY --from=publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "SpaceTrafficETL.dll"]
