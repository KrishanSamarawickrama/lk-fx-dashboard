using LkFxDashboard.Api.Security;
using LkFxDashboard.Core.Extensions;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LkFxDashboard.Api.Endpoints;

public static class RateEndpoints
{
    public static IEndpointRouteBuilder MapRateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rates");

        group.MapGet("/latest", async (
            string? currency,
            IExchangeRateRepository repository,
            CancellationToken cancellationToken) =>
        {
            var targetCurrency = currency?.ToUpperInvariant() ?? "USD";
            var rates = await repository.GetLatestRatesAsync(targetCurrency, cancellationToken);
            return rates.Count > 0
                ? Results.Ok(rates)
                : Results.Ok(Array.Empty<ExchangeRate>());
        }).RequireRateLimiting("api-read");

        group.MapGet("/history", async (
            string? currency,
            int? days,
            IExchangeRateRepository repository,
            CancellationToken cancellationToken) =>
        {
            var targetCurrency = currency?.ToUpperInvariant() ?? "USD";
            var historyDays = days ?? 30;
            var rates = await repository.GetHistoryAsync(targetCurrency, historyDays, cancellationToken);
            return Results.Ok(rates);
        }).RequireRateLimiting("api-read");

        app.MapGet("/api/currencies", async (
            IExchangeRateRepository repository,
            CancellationToken cancellationToken) =>
        {
            var currencies = await repository.GetAvailableCurrenciesAsync(cancellationToken);
            if (currencies.Count == 0)
            {
                // Return default currencies if no data yet
                return Results.Ok(ServiceCollectionExtensions.DefaultCurrencies);
            }

            var currencyInfos = currencies.Select(c =>
            {
                var info = ServiceCollectionExtensions.DefaultCurrencies
                    .FirstOrDefault(d => d.Code == c);
                return info ?? new CurrencyInfo(c, c);
            }).ToList();

            return Results.Ok(currencyInfos);
        }).RequireRateLimiting("api-read");

        app.MapPost("/api/scrape/trigger", async (
            IEnumerable<IExchangeRateScraper> scrapers,
            IExchangeRateRepository repository,
            CancellationToken cancellationToken) =>
        {
            var allRates = new List<ExchangeRate>();

            foreach (var scraper in scrapers)
            {
                try
                {
                    var rates = await scraper.FetchRatesAsync(cancellationToken);
                    allRates.AddRange(rates);
                }
                catch
                {
                    // Individual scraper failures don't fail the whole operation
                }
            }

            if (allRates.Count > 0)
            {
                await repository.AddRangeAsync(allRates, cancellationToken);
            }

            return Results.Ok(new { scraped = allRates.Count });
        }).AddEndpointFilter<ApiKeyEndpointFilter>().RequireRateLimiting("trigger");

        return app;
    }
}
