FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY SpaceTrafficWeb.csproj ./
RUN dotnet restore SpaceTrafficWeb.csproj

FROM node:24-bookworm-slim AS assets
WORKDIR /src

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml vite.config.mjs ./
COPY ClientApp ./ClientApp
RUN corepack enable \
    && corepack prepare pnpm@11.7.0 --activate \
    && pnpm install --frozen-lockfile \
    && pnpm build

FROM restore AS publish
COPY . ./
COPY --from=assets /src/wwwroot/dist ./wwwroot/dist
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
ARG EXASOL_ODBC_DOWNLOAD_PAGE="https://downloads.exasol.com/clients-and-drivers/odbc"

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ODBCSYSINI=/etc \
    ODBCINI=/etc/odbc.ini \
    LD_LIBRARY_PATH=/opt/exasol/odbc

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl tar unzip unixodbc unixodbc-dev \
    && rm -rf /var/lib/apt/lists/*

RUN if [ -n "$EXASOL_ODBC_DRIVER_URL" ] || [ -n "$EXASOL_ODBC_DOWNLOAD_PAGE" ]; then \
        set -eux; \
        mkdir -p /opt/exasol/odbc /tmp/exasol-odbc; \
        driver_url="$EXASOL_ODBC_DRIVER_URL"; \
        if [ -z "$driver_url" ]; then \
            page_base="${EXASOL_ODBC_DOWNLOAD_PAGE%/}"; \
            if echo "$EXASOL_ODBC_DOWNLOAD_PAGE" | grep -Eiq '\.(zip|tar|tgz|tar\.gz)($|\?)'; then \
                driver_url="$EXASOL_ODBC_DOWNLOAD_PAGE"; \
            else \
                curl -fsSL "$EXASOL_ODBC_DOWNLOAD_PAGE" -o /tmp/exasol-odbc-page.html; \
                driver_url="$(grep -Eoi 'href="[^"]+"' /tmp/exasol-odbc-page.html \
                    | sed -E 's/^href="([^"]+)"/\1/i' \
                    | grep -Ei 'linux|x86_64|amd64' \
                    | grep -Ei 'zip|tar|tgz|gz' \
                    | head -n 1 || true)"; \
                if [ -n "$driver_url" ] && ! echo "$driver_url" | grep -Eiq '^https?://'; then \
                    driver_url="${page_base}/${driver_url#/}"; \
                fi; \
            fi; \
        fi; \
        if [ -z "$driver_url" ]; then \
            echo "Could not find Exasol Linux ODBC driver URL. Set EXASOL_ODBC_DRIVER_URL to the direct archive URL."; \
            exit 1; \
        fi; \
        echo "Downloading Exasol ODBC driver from $driver_url"; \
        curl -fsSL "$driver_url" -o /tmp/exasol-odbc-driver; \
        if echo "$driver_url" | grep -Eiq '\.zip($|\?)'; then \
            unzip -q /tmp/exasol-odbc-driver -d /tmp/exasol-odbc; \
        else \
            tar -xf /tmp/exasol-odbc-driver -C /tmp/exasol-odbc; \
        fi; \
        find /tmp/exasol-odbc -type f -name '*.so*' -exec cp {} /opt/exasol/odbc/ \;; \
        driver_path="$(find /opt/exasol/odbc -type f -name '*odbc*.so*' | head -n 1)"; \
        if [ -z "$driver_path" ]; then \
            echo "Downloaded archive did not contain an ODBC .so file."; \
            exit 1; \
        fi; \
        echo /opt/exasol/odbc > /etc/ld.so.conf.d/exasol-odbc.conf; \
        ldconfig; \
        if ldd "$driver_path" | grep -q 'not found'; then \
            ldd "$driver_path"; \
            exit 1; \
        fi; \
        printf '[EXASOL]\nDescription=Exasol ODBC Driver\nDriver=%s\nUsageCount=1\n' "$driver_path" > /etc/odbcinst.ini; \
        rm -rf /tmp/exasol-odbc /tmp/exasol-odbc-driver /tmp/exasol-odbc-page.html; \
    fi

RUN mkdir -p /app/data-protection-keys \
    && chown -R $APP_UID:0 /app/data-protection-keys

COPY --from=publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SpaceTrafficWeb.dll"]
