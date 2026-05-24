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

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled-extra AS final
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

COPY --from=publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "SpaceTrafficETL.dll"]
