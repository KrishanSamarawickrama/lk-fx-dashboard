FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files and project files first for layer caching
COPY global.json .
COPY LkFxDashboard.slnx .
COPY Directory.Build.props .
COPY src/LkFxDashboard.Core/LkFxDashboard.Core.csproj src/LkFxDashboard.Core/
COPY src/LkFxDashboard.Data/LkFxDashboard.Data.csproj src/LkFxDashboard.Data/
COPY src/LkFxDashboard.Scrapers/LkFxDashboard.Scrapers.csproj src/LkFxDashboard.Scrapers/
COPY src/LkFxDashboard.Api/LkFxDashboard.Api.csproj src/LkFxDashboard.Api/
COPY src/LkFxDashboard.ServiceDefaults/LkFxDashboard.ServiceDefaults.csproj src/LkFxDashboard.ServiceDefaults/
COPY src/LkFxDashboard.Web/LkFxDashboard.Web.csproj src/LkFxDashboard.Web/
RUN dotnet restore src/LkFxDashboard.Web/LkFxDashboard.Web.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/LkFxDashboard.Web/LkFxDashboard.Web.csproj \
    -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser appuser \
    && apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "LkFxDashboard.Web.dll"]
