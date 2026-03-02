using LkFxDashboard.Core.Extensions;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using Microsoft.Extensions.Logging;

namespace LkFxDashboard.Web.Services;

public class ServerRateService(
    IExchangeRateRepository repository,
    IEnumerable<IExchangeRateScraper> scrapers,
    ILogger<ServerRateService> logger) : IRateService
{
    public async Task<IReadOnlyList<ExchangeRate>> GetLatestRatesAsync(
        string currency, CancellationToken cancellationToken = default)
    {
        return await repository.GetLatestRatesAsync(currency.ToUpperInvariant(), cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetHistoryAsync(
        string currency, int days = 30, CancellationToken cancellationToken = default)
    {
        return await repository.GetHistoryAsync(currency.ToUpperInvariant(), days, cancellationToken);
    }

    public async Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(
        CancellationToken cancellationToken = default)
    {
        var currencies = await repository.GetAvailableCurrenciesAsync(cancellationToken);
        if (currencies.Count == 0)
        {
            return ServiceCollectionExtensions.DefaultCurrencies;
        }

        return currencies.Select(c =>
        {
            var info = ServiceCollectionExtensions.DefaultCurrencies
                .FirstOrDefault(d => d.Code == c);
            return info ?? new CurrencyInfo(c, c);
        }).ToList();
    }

    public async Task<int> RefreshRatesAsync(CancellationToken cancellationToken = default)
    {
        var allRates = new List<ExchangeRate>();

        foreach (var scraper in scrapers)
        {
            try
            {
                logger.LogInformation("Refreshing rates from {Source}...", scraper.SourceName);
                var rates = await scraper.FetchRatesAsync(cancellationToken);
                logger.LogInformation("{Source} returned {Count} rates", scraper.SourceName, rates.Count);
                allRates.AddRange(rates);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error refreshing rates from {Source}", scraper.SourceName);
            }
        }

        if (allRates.Count > 0)
        {
            try
            {
                await repository.AddRangeAsync(allRates, cancellationToken);
                logger.LogInformation("Refresh complete: {Count} total rates saved to database", allRates.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save {Count} rates to database", allRates.Count);
            }
        }

        return allRates.Count;
    }
}
