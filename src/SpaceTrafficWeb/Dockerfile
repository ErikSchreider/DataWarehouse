FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY SpaceTrafficWeb.csproj ./
RUN dotnet restore SpaceTrafficWeb.csproj

FROM restore AS publish
COPY . ./
RUN dotnet publish SpaceTrafficWeb.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
ARG EXASOL_ODBC_DRIVER_URL=""

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ODBCSYSINI=/etc \
    ODBCINI=/etc/odbc.ini

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl tar unzip unixodbc \
    && rm -rf /var/lib/apt/lists/*

RUN if [ -n "$EXASOL_ODBC_DRIVER_URL" ]; then \
        mkdir -p /opt/exasol/odbc /tmp/exasol-odbc \
        && curl -fsSL "$EXASOL_ODBC_DRIVER_URL" -o /tmp/exasol-odbc-driver \
        && if echo "$EXASOL_ODBC_DRIVER_URL" | grep -Eiq '\.zip($|\?)'; then \
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

RUN mkdir -p /app/data-protection-keys \
    && chown -R $APP_UID:0 /app/data-protection-keys

COPY --from=publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SpaceTrafficWeb.dll"]
