# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2025-03-02

### Added

- Daily FX rate scraping from four Sri Lankan banking sources
  - Standard Chartered (PDF parsing via PdfPig)
  - CBSL / Central Bank (HTML POST via AngleSharp)
  - Commercial Bank (HTML GET via AngleSharp)
  - HSBC (PDF parsing via PdfPig)
- Blazor SSR dashboard with dark theme
  - Rate summary cards with trend indicators
  - Multi-source history chart (ApexCharts) with 30/60/90 day toggle
  - Source comparison table highlighting best rates
  - Currency selector (USD, EUR, GBP, AUD, CAD, SGD, CHF, JPY, INR)
  - Manual scrape trigger with admin PIN
  - Stale data indicator (>25 hours)
- REST API endpoints (`/api/rates/latest`, `/api/rates/history`, `/api/currencies`, `/api/scrape/trigger`)
- API key authentication and rate limiting for write endpoints
- Automated daily scraping at 08:00 SLST (UTC+5:30) via background service
- PostgreSQL storage with EF Core and .NET Aspire orchestration
- Docker deployment support (`deployment/docker-compose.yml`)
- OpenTelemetry observability and health checks
- 27 unit and integration tests (xUnit v3)

[0.1.0]: https://github.com/krishans1990/lk-fx-dashboard/releases/tag/v0.1.0
