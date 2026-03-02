# LK FX Dashboard

A daily exchange rate monitoring dashboard built with .NET 10, Blazor, and .NET Aspire. Scrapes FX rates from 4 Sri Lankan bank/financial sources (Standard Chartered, CBSL, Commercial Bank, HSBC) and displays current + historical data via a dark-themed dashboard with ApexCharts.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Aspire-managed PostgreSQL container)

## Quick Start

```bash
# Clone and run
git clone <repo-url>
cd lk-fx-dashboard
dotnet run --project src/LkFxDashboard.AppHost
```

Aspire will automatically:
1. Start a PostgreSQL container
2. Apply database migrations
3. Launch the web dashboard
4. Open the Aspire dashboard for service health and telemetry

**App**: `http://localhost:5000`
**Aspire Dashboard**: `http://localhost:15888`

## Architecture

```
LkFxDashboard.AppHost          <- Aspire orchestrator (startup project)
  |
  +-- PostgreSQL (container)   <- Managed by Aspire
  +-- LkFxDashboard.Web       <- Blazor Web App (single host)
        |
        +-- ServiceDefaults    <- Telemetry, health checks, resilience
        +-- Api                <- Minimal API endpoints + background service
        +-- Scrapers           <- 4 scraper implementations
        +-- Data               <- EF Core + PostgreSQL
        +-- Core               <- Domain models + interfaces
```

**Single-host architecture**: The Web project hosts Blazor SSR, minimal API endpoints, and the background scraping service. Blazor components inject services directly (no HTTP self-calls).

## Data Sources

| Source | Method | Library |
|--------|--------|---------|
| Standard Chartered LK | HTML scraping | AngleSharp |
| CBSL (Central Bank) | HTML scraping | AngleSharp |
| Commercial Bank (COMBANK) | HTML scraping | AngleSharp |
| HSBC LK | PDF parsing | PdfPig |

## API Endpoints

```
GET  /api/rates/latest?currency=CHF      Latest rates from all sources
GET  /api/rates/history?currency=CHF&days=30  Historical rates
GET  /api/currencies                      List of tracked currencies
POST /api/scrape/trigger                  Manual scrape trigger
```

## Dashboard Features

- **Rate Summary Cards** - Today's buy/sell rate per source with trend indicators
- **History Chart** - Multi-source line chart with 30/60/90 day toggle (ApexCharts)
- **Source Comparison Table** - Side-by-side comparison highlighting best rates
- **Currency Selector** - Switch between tracked currencies
- **Refresh Button** - Trigger manual scrape with loading spinner
- **Last Updated Badge** - Stale data indicator (pulses amber if >25h old)

## Configuration

Edit `src/LkFxDashboard.Web/appsettings.json`:

```json
{
  "Scraping": {
    "StandardCharteredUrl": "https://www.sc.com/lk/...",
    "CbslUrl": "https://www.cbsl.gov.lk/...",
    "ComBankUrl": "https://www.combank.lk/rates-tariff#exchange-rates",
    "HsbcPdfUrl": "https://www.hsbc.lk/...",
    "TimeoutSeconds": 30,
    "PoliteDelayMs": 2000
  }
}
```

## How to Add a New Currency

1. Add the currency code to each scraper's `SupportedCurrencies` list
2. Add a `CurrencyInfo` entry in `Core/Extensions/ServiceCollectionExtensions.cs`
3. The dashboard and API will automatically pick up the new currency

## How to Add a New Data Source

1. Create a new scraper class implementing `IExchangeRateScraper` in `Scrapers/Scrapers/`
2. Add a constant to `Core/Models/ScraperSource.cs`
3. Register the typed HttpClient + scraper in `ScraperServiceCollectionExtensions.cs`
4. Add a test fixture and test class in `tests/`

## Running Tests

```bash
dotnet test
```

27 tests covering scrapers (unit), repository (integration with InMemory), and API endpoints (WebApplicationFactory).

## Tech Stack

- .NET 10 / C# 14
- .NET Aspire (orchestration, telemetry, health checks)
- Blazor Server (interactive SSR)
- PostgreSQL via Aspire container
- Entity Framework Core 10
- ApexCharts (Blazor-ApexCharts)
- Tailwind CSS v4
- Serilog structured logging
- AngleSharp, PdfPig (scrapers)
