using AngleSharp;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes TT exchange rates from Bank of Ceylon.
/// The page is server-rendered HTML — no JS rendering required.
/// Table columns: Currency | Drafts (Buy/Sell) | TT (Buy/Sell) | Cash (Buy/Sell)
/// Currencies are identified by 3-letter ISO codes.
/// Falls back to Drafts rates when TT rates are unavailable ("-").
/// </summary>
public class BocScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<BocScraper> logger) : IExchangeRateScraper
{
    private static readonly HashSet<string> KnownCurrencyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "JPY", "SGD", "AUD", "CHF", "CAD", "NZD",
        "INR", "AED", "SAR", "KWD", "BHD", "QAR", "OMR", "SEK", "NOK",
        "DKK", "HKD", "THB", "CNY", "MYR", "PKR", "KRW", "TWD", "IDR"
    };

    public string SourceName => ScraperSource.Boc;

    public IReadOnlyList<string> SupportedCurrencies =>
        ["USD", "EUR", "GBP", "JPY", "SGD", "AUD", "CHF", "CAD", "NZD",
         "INR", "AED", "SAR", "KWD", "BHD", "QAR", "OMR", "SEK", "NOK",
         "DKK", "HKD", "THB", "CNY", "MYR", "PKR", "KRW", "TWD", "IDR"];

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching rates from Bank of Ceylon: {Url}", config.BocUrl);

            await Task.Delay(config.PoliteDelayMs, cancellationToken);

            var response = await httpClient.GetAsync(config.BocUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return await ParseHtmlAsync(html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Bank of Ceylon rates");
            return [];
        }
    }

    internal async Task<IReadOnlyList<ExchangeRate>> ParseHtmlAsync(string html)
    {
        var rates = new List<ExchangeRate>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html));

        var tables = document.QuerySelectorAll("table");

        foreach (var table in tables)
        {
            var rows = table.QuerySelectorAll("tbody tr");
            if (rows.Length == 0)
                rows = table.QuerySelectorAll("tr");

            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                // Expected: Currency | Drafts Buy | Drafts Sell | TT Buy | TT Sell | Cash Buy | Cash Sell
                if (cells.Length < 3) continue;

                var currencyCode = ExtractCurrencyCode(cells[0].TextContent);
                if (currencyCode is null) continue;

                // Try TT rates first (columns 3-4), fall back to Drafts (columns 1-2)
                if (cells.Length >= 5 &&
                    TryParseDecimal(cells[3].TextContent, out var ttBuy) &&
                    TryParseDecimal(cells[4].TextContent, out var ttSell))
                {
                    rates.Add(CreateRate(currencyCode, ttBuy, ttSell, today));
                }
                else if (TryParseDecimal(cells[1].TextContent, out var draftBuy) &&
                         TryParseDecimal(cells[2].TextContent, out var draftSell))
                {
                    rates.Add(CreateRate(currencyCode, draftBuy, draftSell, today));
                }
            }

            if (rates.Count > 0) break;
        }

        logger.LogInformation("Bank of Ceylon: parsed {Count} rates", rates.Count);
        return rates;
    }

    private ExchangeRate CreateRate(string currencyCode, decimal buyRate, decimal sellRate, DateOnly rateDate)
    {
        return new ExchangeRate
        {
            BaseCurrency = "LKR",
            TargetCurrency = currencyCode,
            BuyingRate = buyRate,
            SellingRate = sellRate,
            Source = SourceName,
            CollectedAt = DateTime.UtcNow,
            RateDate = rateDate
        };
    }

    private static string? ExtractCurrencyCode(string text)
    {
        // BOC uses 3-letter ISO codes (e.g. "USD", "EUR") in the currency cell
        var trimmed = text.Trim().ToUpperInvariant();

        // The cell may contain extra text around the code; find the 3-letter code
        foreach (var word in trimmed.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length == 3 && KnownCurrencyCodes.Contains(word))
                return word;
        }

        return null;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        var cleaned = text.Trim().Replace(",", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
    }
}
