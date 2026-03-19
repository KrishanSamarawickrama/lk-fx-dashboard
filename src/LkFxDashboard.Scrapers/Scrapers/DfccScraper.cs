using AngleSharp;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes TT exchange rates from DFCC Bank.
/// The page is server-rendered HTML — no JS rendering required.
/// Table columns: Currency | DD Buying | Currency Note Encashment | TT Buying | DD/TT Selling | Currency Note Selling
/// Currency codes are extracted from the img[alt] attribute in the first cell.
/// </summary>
public class DfccScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<DfccScraper> logger) : IExchangeRateScraper
{
    public string SourceName => ScraperSource.Dfcc;

    public IReadOnlyList<string> SupportedCurrencies =>
        ["USD", "GBP", "JPY", "CHF", "SGD", "CAD", "HKD", "AUD", "SEK", "NZD",
         "EUR", "CNY", "THB", "MYR", "DKK", "NOK", "AED", "SAR", "QAR", "KWD",
         "OMR", "INR"];

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching rates from DFCC Bank: {Url}", config.DfccUrl);

            await Task.Delay(config.PoliteDelayMs, cancellationToken);

            var response = await httpClient.GetAsync(config.DfccUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return await ParseHtmlAsync(html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch DFCC Bank rates");
            return [];
        }
    }

    internal async Task<IReadOnlyList<ExchangeRate>> ParseHtmlAsync(string html)
    {
        var rates = new List<ExchangeRate>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var supported = new HashSet<string>(SupportedCurrencies, StringComparer.OrdinalIgnoreCase);

        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html));

        var rows = document.QuerySelectorAll("table.table tbody tr");

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            // Expected columns: Currency | DD Buying | Note Encashment | TT Buying | DD/TT Selling | Note Selling
            if (cells.Length < 5) continue;

            // Currency code is the alt text of the flag img in the first cell
            var img = cells[0].QuerySelector("img");
            if (img is null) continue;

            var currencyCode = img.GetAttribute("alt")?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(currencyCode) || !supported.Contains(currencyCode)) continue;

            // TT Buying is column index 3, DD/TT Selling is column index 4
            if (TryParseDecimal(cells[3].TextContent, out var ttBuy) &&
                TryParseDecimal(cells[4].TextContent, out var ttSell))
            {
                rates.Add(new ExchangeRate
                {
                    BaseCurrency = "LKR",
                    TargetCurrency = currencyCode,
                    BuyingRate = ttBuy,
                    SellingRate = ttSell,
                    Source = SourceName,
                    CollectedAt = DateTime.UtcNow,
                    RateDate = today
                });
            }
        }

        logger.LogInformation("DFCC Bank: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        var cleaned = text.Trim().Replace(",", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
    }
}
