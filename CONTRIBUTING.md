# Contributing to LK FX Dashboard

Thank you for your interest in contributing! This document covers how to get the project running locally, the conventions we follow, and the pull request process.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Running Tests](#running-tests)
- [Adding a Scraper](#adding-a-scraper)
- [Pull Request Process](#pull-request-process)
- [Code Style](#code-style)

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Aspire manages the PostgreSQL container)
- Any C# IDE — Visual Studio 2022, VS Code with C# Dev Kit, or Rider

## Development Setup

```bash
# 1. Fork and clone
git clone https://github.com/krishans1990/lk-fx-dashboard.git
cd lk-fx-dashboard

# 2. Start the full stack (Aspire orchestrator)
dotnet run --project src/LkFxDashboard.AppHost
```

Aspire will start a PostgreSQL 17 container, apply migrations, and launch the web app. The dashboard is at `http://localhost:5000` and the Aspire telemetry dashboard at `http://localhost:15888`.

To stop: `Ctrl+C` in the terminal. The PostgreSQL container is cleaned up automatically by Aspire on exit.

**Configuration:** Copy sensitive defaults before starting:

```bash
# Optional: override appsettings locally (gitignored)
cp src/LkFxDashboard.Web/appsettings.json \
   src/LkFxDashboard.Web/appsettings.Development.json
# Then edit appsettings.Development.json with your local values
```

> Never commit real secrets. `appsettings.*.local.json` and `deployment/.env` are gitignored.

## Project Structure

```
src/
  LkFxDashboard.AppHost/        ← Aspire orchestrator (startup project)
  LkFxDashboard.Web/
    Components/                 ← Blazor pages and UI components
    Core/                       ← Domain models (ExchangeRate, CurrencyInfo), interfaces
    Data/                       ← EF Core DbContext, repository, migrations
    Scrapers/                   ← IExchangeRateScraper implementations
    Api/                        ← Minimal API endpoints + RateScrapingBackgroundService
    Middleware/                 ← Security headers middleware
  LkFxDashboard.ServiceDefaults/ ← Shared OpenTelemetry / health check setup
tests/
  LkFxDashboard.Tests/
    Fixtures/                   ← Embedded HTML and PDF files used in tests
    Scrapers/                   ← Scraper unit tests
    Repository/                 ← Repository integration tests (InMemory EF Core)
    Api/                        ← API endpoint tests (WebApplicationFactory)
deployment/
  Dockerfile
  docker-compose.yml
  .env.example
  build-and-push.ps1
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "FullyQualifiedName~CbslScraperTests"

# Run a specific test method
dotnet test --filter "Name=ParseHtml_ReturnsNonEmptyResults"
```

Tests do not require Docker or a live database — scrapers are tested against embedded fixture files, and repository/API tests use EF Core InMemory.

## Adding a Scraper

1. Implement `IExchangeRateScraper` in `src/LkFxDashboard.Web/Scrapers/Scrapers/`
2. Add a source constant to `Core/Models/ScraperSource.cs`
3. Register a typed `HttpClient` + transient factory in `ScraperServiceCollectionExtensions.cs`
4. Add a URL key to `ScraperOptions.cs` and a default value in `appsettings.json`
5. Add a fixture file (real HTML/PDF snapshot) to `tests/LkFxDashboard.Tests/Fixtures/`
6. Write a test class covering `ParseHtmlAsync` or `ParsePdfAsync` with that fixture

See existing scrapers (e.g. `CbslScraper`, `ComBankScraper`) for reference patterns.

## Pull Request Process

1. **Branch** from `main`: `git checkout -b feat/my-feature` or `fix/my-bug`
2. **Keep PRs focused** — one feature or fix per PR
3. **Tests required** — new scrapers and API changes must include tests; all existing tests must pass (`dotnet test`)
4. **No secrets** — never commit API keys, passwords, or real `.env` files
5. **Update docs** if you change behaviour visible in the README (new endpoints, new config keys, etc.)
6. Open the PR against `main` and fill in the PR template

A maintainer will review and merge. For large changes, open an issue first to discuss the approach.

## Code Style

The project uses the .NET SDK defaults plus:

- **Nullable reference types enabled** — annotate all new code; avoid `!` suppressions
- **Implicit usings enabled** — no need to add `using System;` etc.
- **No trailing whitespace**
- **Async all the way** — don't block async code with `.Result` or `.Wait()`
- **Internal visibility for testability** — scraper internals are exposed via `InternalsVisibleTo` in the Scrapers project; use `internal` for implementation details rather than making everything `public`

The `.editorconfig` at the repository root defines formatting rules. Your IDE should pick these up automatically.
