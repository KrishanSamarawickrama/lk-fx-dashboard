# LK FX Dashboard

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![CI](https://github.com/KrishanSamarawickrama/lk-fx-dashboard/actions/workflows/ci.yml/badge.svg)](https://github.com/krishans1990/lk-fx-dashboard/actions/workflows/ci.yml)

A daily foreign exchange rate monitoring dashboard for Sri Lanka. Scrapes buy/sell rates from four banking sources, stores them in PostgreSQL, and presents current and historical data in a dark-themed Blazor dashboard with interactive charts.

---

## Features

- **Rate Summary Cards** — Today's buy/sell rate per source with trend indicators
- **History Chart** — Multi-source line chart with 30/60/90 day toggle (ApexCharts)
- **Source Comparison Table** — Side-by-side comparison highlighting best rates
- **Currency Selector** — Switch between tracked currencies (USD, EUR, GBP, AUD, CAD, SGD, CHF, JPY, INR)
- **Manual Refresh** — Trigger a scrape on demand with a loading spinner
- **Stale Data Indicator** — Last Updated badge pulses amber if data is >25 hours old
- **REST API** — JSON endpoints for external consumers
- **Automated Scraping** — Daily at 08:00 SLST (UTC+5:30) via background service

## Data Sources

| Source | Scraper Class | Method | Library |
|--------|--------------|--------|---------|
| Standard Chartered LK | `ScStandardCharteredScraper` | PDF download + parsing | PdfPig |
| CBSL (Central Bank of Sri Lanka) | `CbslScraper` | HTML POST form + parsing | AngleSharp |
| Commercial Bank (ComBank) | `ComBankScraper` | HTML GET + parsing | AngleSharp |
| HSBC LK | `HsbcPdfScraper` | PDF download + parsing | PdfPig |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10 / C# 14 |
| Web UI | Blazor Server (SSR) |
| Styling | Tailwind CSS v4 |
| Charting | ApexCharts (Blazor-ApexCharts) |
| Database | PostgreSQL 17 |
| ORM | Entity Framework Core 10 + Npgsql |
| Orchestration | .NET Aspire 9 |
| Scraping | AngleSharp (HTML), PdfPig (PDF) |
| Logging | Serilog |
| Observability | OpenTelemetry |
| Testing | xUnit v3, NSubstitute, FluentAssertions |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for the Aspire-managed PostgreSQL container)

## Quick Start

```bash
git clone https://github.com/krishans1990/lk-fx-dashboard.git
cd lk-fx-dashboard
dotnet run --project src/LkFxDashboard.AppHost
```

Aspire will automatically:
1. Pull and start a PostgreSQL 17 container
2. Apply database migrations
3. Start the Blazor web app and background scraper
4. Open the Aspire dashboard for service health and telemetry

| URL | Description |
|-----|-------------|
| `http://localhost:5000` | FX Dashboard |
| `http://localhost:15888` | Aspire Dashboard (telemetry, health, logs) |

> On first run the dashboard will show no data until the background scraper completes (allow ~30 seconds).

## Architecture

```
LkFxDashboard.AppHost          ← Aspire orchestrator (startup project)
  │
  ├── PostgreSQL (container)   ← Managed by Aspire
  └── LkFxDashboard.Web        ← Single-host Blazor + API + Background Service
        ├── Core               ← Domain models (ExchangeRate, CurrencyInfo), interfaces
        ├── Data               ← EF Core DbContext, repository, migrations
        ├── Scrapers           ← 4 scraper implementations
        ├── Api                ← Minimal API endpoints + RateScrapingBackgroundService
        └── ServiceDefaults    ← OpenTelemetry, health checks, resilience
```

**Single-host design:** The Web project co-hosts Blazor SSR, the REST API, and the background scraping service. Blazor components inject services directly — no HTTP self-calls.

**Data flow:**
1. `RateScrapingBackgroundService` runs on startup then daily at 08:00 SLST
2. Each `IExchangeRateScraper` fetches rates → repository persists to PostgreSQL
3. Blazor pages and API endpoints read from the same repository

## Configuration

All configuration is in `src/LkFxDashboard.Web/appsettings.json`. For local development you can override values in `appsettings.Development.json`.

```json
{
  "Scraping": {
    "StandardCharteredUrl": "https://av.sc.com/lk/content/docs/lk-exchange-rates.pdf",
    "CbslUrl": "https://www.cbsl.gov.lk/cbsl_custom/exratestt/exrates_resultstt.php",
    "ComBankUrl": "https://www.combank.lk/rates-tariff#exchange-rates",
    "HsbcPdfUrl": "https://www.hsbc.lk/content/dam/hsbc/lk/documents/tariffs/foreign-exchange-rates.pdf",
    "TimeoutSeconds": 30,
    "PoliteDelayMs": 2000
  },
  "Security": {
    "ApiKey": "CHANGE-ME-TO-A-RANDOM-SECRET",
    "AdminPin": "1234"
  }
}
```

> **Security:** Change `ApiKey` and `AdminPin` before deploying. The API key is required in the `X-Api-Key` header for write endpoints. The admin PIN controls the manual scrape trigger on the dashboard.

## Docker Deployment

A production-ready `docker-compose.yml` is provided in `deployment/`.

```bash
cd deployment
cp .env.example .env          # copy and edit secrets
# Edit .env: set POSTGRES_PASSWORD, SECURITY_API_KEY, SECURITY_ADMIN_PIN, DOCKER_IMAGE
docker compose up -d
```

The compose file starts:
- **PostgreSQL 17** with a named volume for data persistence
- **LK FX Dashboard** web app (non-root, health-checked)

To build and push the Docker image:

```powershell
# From the deployment/ directory
.\build-and-push.ps1
```

## API Reference

All read endpoints are public (rate-limited to 30 req/min per IP). The trigger endpoint requires the `X-Api-Key` header and is rate-limited to 2 req/hour per IP.

```
GET  /api/rates/latest?currency=USD          Latest rates from all sources
GET  /api/rates/history?currency=USD&days=30 Historical rates (max 90 days)
GET  /api/currencies                         List of tracked currencies
POST /api/scrape/trigger                     Trigger a manual scrape (requires X-Api-Key)
GET  /health                                 Health check endpoint
```

## Running Tests

```bash
dotnet test
```

27 tests across three categories:
- **Scraper unit tests** — Parse HTML/PDF fixtures without network calls
- **Repository integration tests** — EF Core InMemory database
- **API endpoint tests** — `WebApplicationFactory` with in-memory storage

```bash
# Run a specific test class
dotnet test --filter "FullyQualifiedName~CbslScraperTests"

# Run a specific test method
dotnet test --filter "Name=ParseHtml_ReturnsNonEmptyResults"
```

## Extending the Dashboard

### Adding a New Currency

1. Add the currency code to each scraper's `SupportedCurrencies` list
2. Add a `CurrencyInfo` entry in `Core/Extensions/ServiceCollectionExtensions.cs`
3. The dashboard and API pick it up automatically — no other changes needed

### Adding a New Data Source

1. Implement `IExchangeRateScraper` in `src/LkFxDashboard.Web/Scrapers/Scrapers/`
2. Add a source constant to `Core/Models/ScraperSource.cs`
3. Register a typed `HttpClient` + transient factory in `ScraperServiceCollectionExtensions.cs`
4. Add a URL to `ScraperOptions.cs` and `appsettings.json`
5. Add a fixture file and test class in `tests/LkFxDashboard.Tests/`

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, coding conventions, and the pull request process.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating you agree to uphold these standards.

## License

[MIT](LICENSE) — © 2025 LK FX Dashboard Contributors
