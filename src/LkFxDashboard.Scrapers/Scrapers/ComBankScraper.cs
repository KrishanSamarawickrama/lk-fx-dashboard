using AngleSharp;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes TT exchange rates from Commercial Bank of Ceylon.
/// The page is server-rendered HTML — no JS rendering required.
/// Table columns: Currency | Cheques (Buy/Sell) | TT (Buy/Sell) | Cash (Buy/Sell)
/// </summary>
public class ComBankScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<ComBankScraper> logger) : IExchangeRateScraper
{
    private static readonly Dictionary<string, string> CurrencyNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US DOLLARS"] = "USD",
        ["US DOLLAR"] = "USD",
        ["EURO"] = "EUR",
        ["STERLING POUNDS"] = "GBP",
        ["STERLING POUND"] = "GBP",
        ["JAPANESE YEN"] = "JPY",
        ["SINGAPORE DOLLARS"] = "SGD",
        ["SINGAPORE DOLLAR"] = "SGD",
        ["AUSTRALIAN DOLLARS"] = "AUD",
        ["AUSTRALIAN DOLLAR"] = "AUD",
        ["SWISS FRANCS"] = "CHF",
        ["SWISS FRANC"] = "CHF",
        ["CANADIAN DOLLAR"] = "CAD",
        ["CANADIAN DOLLARS"] = "CAD",
        ["NEW ZEALAND DOLLARS"] = "NZD",
        ["NEW ZEALAND DOLLAR"] = "NZD",
        ["INDIAN RUPEES"] = "INR",
        ["INDIAN RUPEE"] = "INR",
        ["KUWAITI DINARS"] = "KWD",
        ["KUWAITI DINAR"] = "KWD",
        ["BAHRAIN DINARS"] = "BHD",
        ["BAHRAIN DINAR"] = "BHD",
        ["SAUDI ARABIAN RIYALS"] = "SAR",
        ["SAUDI ARABIAN RIYAL"] = "SAR",
        ["QATAR RIYALS"] = "QAR",
        ["QATAR RIYAL"] = "QAR",
        ["UAE DIRHAMS"] = "AED",
        ["UAE DIRHAM"] = "AED",
        ["OMANI RIYALS"] = "OMR",
        ["OMANI RIYAL"] = "OMR",
        ["JORDANIAN DINARS"] = "JOD",
        ["JORDANIAN DINAR"] = "JOD",
    };

    public string SourceName => ScraperSource.ComBank;

    public IReadOnlyList<string> SupportedCurrencies =>
        ["USD", "EUR", "GBP", "JPY", "SGD", "AUD", "CHF", "CAD", "NZD",
         "INR", "KWD", "BHD", "SAR", "QAR", "AED", "OMR", "JOD"];

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching rates from Commercial Bank: {Url}", config.ComBankUrl);

            await Task.Delay(config.PoliteDelayMs, cancellationToken);

            var response = await httpClient.GetAsync(config.ComBankUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return await ParseHtmlAsync(html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Commercial Bank rates");
            return [];
        }
    }

    internal async Task<IReadOnlyList<ExchangeRate>> ParseHtmlAsync(string html)
    {
        var rates = new List<ExchangeRate>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html));

        // Find all tables on the page — the exchange rate table has currency rows
        var tables = document.QuerySelectorAll("table");

        foreach (var table in tables)
        {
            var rows = table.QuerySelectorAll("tbody tr");
            if (rows.Length == 0)
                rows = table.QuerySelectorAll("tr");

            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                // Expected columns: Currency | Cheques Buy | Cheques Sell | TT Buy | TT Sell | Cash Buy | Cash Sell
                if (cells.Length < 5) continue;

                var currencyText = cells[0].TextContent.Trim();
                var currencyCode = MapCurrencyNameToCode(currencyText);
                if (currencyCode is null) continue;

                // TT Buying is column index 3, TT Selling is column index 4
                // (0=Currency, 1=Cheques Buy, 2=Cheques Sell, 3=TT Buy, 4=TT Sell, ...)
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

            // If we found rates from this table, stop looking at other tables
            if (rates.Count > 0) break;
        }

        logger.LogInformation("Commercial Bank: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static string? MapCurrencyNameToCode(string name)
    {
        var trimmed = name.Trim();
        if (CurrencyNameToCode.TryGetValue(trimmed, out var code))
            return code;

        // Fuzzy match: check if any key is contained in the name
        foreach (var (key, value) in CurrencyNameToCode)
        {
            if (trimmed.Contains(key, StringComparison.OrdinalIgnoreCase))
                return value;
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
