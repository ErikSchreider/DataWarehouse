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

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ODBCSYSINI=/etc \
    ODBCINI=/etc/odbc.ini

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates unixodbc \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SpaceTrafficWeb.dll"]
