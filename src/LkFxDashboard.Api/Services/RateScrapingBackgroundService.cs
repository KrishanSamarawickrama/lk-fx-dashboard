using LkFxDashboard.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LkFxDashboard.Api.Services;

public class RateScrapingBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RateScrapingBackgroundService> logger) : BackgroundService
{
    private static readonly TimeZoneInfo SriLankaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    private const int ScheduledHour = 8; // 08:00 SLST

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Rate scraping background service started");

        // Run an initial scrape on startup
        await RunScrapeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNext();
            logger.LogInformation(
                "Next scrape scheduled in {Hours:F1} hours at 08:00 SLST",
                delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await RunScrapeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Rate scraping background service stopped");
    }

    private async Task RunScrapeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting scheduled rate scrape");

        using var scope = scopeFactory.CreateScope();
        var scrapers = scope.ServiceProvider.GetServices<IExchangeRateScraper>();
        var repository = scope.ServiceProvider.GetRequiredService<IExchangeRateRepository>();

        var totalRates = 0;

        foreach (var scraper in scrapers)
        {
            var count = await TryScrapeWithRetryAsync(
                scraper, repository, cancellationToken);

            if (count == 0)
            {
                logger.LogWarning("Scraper {Source} failed after retry or returned 0 rates", scraper.SourceName);
            }

            totalRates += count;
        }

        logger.LogInformation("Scheduled scrape completed. Total rates collected: {Count}", totalRates);
    }

    private async Task<int> TryScrapeWithRetryAsync(
        IExchangeRateScraper scraper,
        IExchangeRateRepository repository,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    logger.LogInformation(
                        "Retrying {Source} after 30 minute delay", scraper.SourceName);
                    await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);
                }

                var rates = await scraper.FetchRatesAsync(cancellationToken);
                if (rates.Count > 0)
                {
                    await repository.AddRangeAsync(rates, cancellationToken);
                    logger.LogInformation(
                        "Scraped and saved {Count} rates from {Source}",
                        rates.Count, scraper.SourceName);
                    return rates.Count;
                }

                logger.LogWarning("No rates returned from {Source}", scraper.SourceName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error scraping/saving {Source} (attempt {Attempt})",
                    scraper.SourceName, attempt + 1);
            }
        }

        return 0;
    }

    private static TimeSpan CalculateDelayUntilNext()
    {
        var nowUtc = DateTime.UtcNow;
        var nowSl = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, SriLankaTimeZone);

        var nextRun = nowSl.Date.AddHours(ScheduledHour);
        if (nowSl >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRun, SriLankaTimeZone);
        return nextRunUtc - nowUtc;
    }
}
