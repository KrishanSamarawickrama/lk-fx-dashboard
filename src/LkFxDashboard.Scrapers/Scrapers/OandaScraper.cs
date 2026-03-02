using AngleSharp;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes mid-market exchange rates from OANDA.
/// OANDA is JS-rendered so plain HTTP does not work — PuppeteerSharp is required.
/// Disabled by default (set Scraping:OandaEnabled=true to enable).
/// On first run, PuppeteerSharp downloads ~280MB Chromium.
/// </summary>
public class OandaScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<OandaScraper> logger) : IExchangeRateScraper
{
    public string SourceName => ScraperSource.Oanda;

    public IReadOnlyList<string> SupportedCurrencies =>
        ["USD", "EUR", "GBP", "CHF", "JPY", "AUD", "CAD", "SGD"];

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;

            if (!config.OandaEnabled)
            {
                logger.LogInformation("OANDA scraping is disabled (set Scraping:OandaEnabled=true to enable)");
                return [];
            }

            logger.LogInformation("Fetching rates from OANDA (PuppeteerSharp)");
            return await FetchWithPuppeteerAsync(config, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch OANDA rates");
            return [];
        }
    }

    private async Task<IReadOnlyList<ExchangeRate>> FetchWithPuppeteerAsync(
        ScraperOptions config, CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading Chromium if needed (first run only)...");

        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LkFxDashboard", "puppeteer");
        var browserFetcher = new PuppeteerSharp.BrowserFetcher(new PuppeteerSharp.BrowserFetcherOptions
        {
            Path = cachePath
        });
        await browserFetcher.DownloadAsync();

        await using var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(
            new PuppeteerSharp.LaunchOptions
            {
                Headless = true,
                ExecutablePath = browserFetcher.GetInstalledBrowsers().First().GetExecutablePath()
            });

        var rates = new List<ExchangeRate>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var currency in SupportedCurrencies)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var page = await browser.NewPageAsync();
                var url = $"https://www.oanda.com/currency-converter/en/?from={currency}&to=LKR&amount=1";
                await page.GoToAsync(url, new PuppeteerSharp.NavigationOptions
                {
                    WaitUntil = [PuppeteerSharp.WaitUntilNavigation.Networkidle2],
                    Timeout = config.TimeoutSeconds * 1000
                });

                // Try multiple selectors for the rate value
                var rateText = await page.EvaluateFunctionAsync<string>("""
                    () => {
                        const selectors = [
                            '[data-testid="result-rate"]',
                            '.converter-result .rate-value',
                            '.result-rate',
                            '#result-rate'
                        ];
                        for (const sel of selectors) {
                            const el = document.querySelector(sel);
                            if (el && el.textContent.trim()) return el.textContent.trim();
                        }
                        return '';
                    }
                    """);

                if (!string.IsNullOrEmpty(rateText) && TryParseDecimal(rateText, out var rate))
                {
                    rates.Add(new ExchangeRate
                    {
                        BaseCurrency = "LKR",
                        TargetCurrency = currency,
                        BuyingRate = rate,
                        SellingRate = rate, // OANDA provides mid-market rate only
                        Source = SourceName,
                        CollectedAt = DateTime.UtcNow,
                        RateDate = today
                    });
                }

                // Polite delay between page loads
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch OANDA rate for {Currency}", currency);
            }
        }

        logger.LogInformation("OANDA: fetched {Count} rates", rates.Count);
        return rates;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        // Extract only digits and decimal point from the rate text
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
    }
}
