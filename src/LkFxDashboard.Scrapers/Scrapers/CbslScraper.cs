using AngleSharp;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes exchange rates from the Central Bank of Sri Lanka.
/// Uses a direct POST to the TT buy/sell rates endpoint, which returns HTML tables.
/// Falls back to a 7-day date range if today's data isn't available yet.
/// </summary>
public class CbslScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<CbslScraper> logger) : IExchangeRateScraper
{
    // CBSL form values: "CODE~Full Name" as expected by the PHP endpoint
    private static readonly Dictionary<string, string> CurrencyFormValues = new()
    {
        ["USD"] = "USD~United States Dollar",
        ["EUR"] = "EUR~Euro",
        ["GBP"] = "GBP~British Pound",
        ["CHF"] = "CHF~Swiss Franc",
        ["JPY"] = "JPY~Yen",
        ["AUD"] = "AUD~Australian Dollar",
        ["CAD"] = "CAD~Canadian Dollar",
        ["SGD"] = "SGD~Singapore Dollar",
        ["CNY"] = "CNY~Renminbi"
    };

    public string SourceName => ScraperSource.Cbsl;

    public IReadOnlyList<string> SupportedCurrencies => CurrencyFormValues.Keys.ToList();

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching rates from CBSL: {Url}", config.CbslUrl);

            // Polite delay before accessing government site
            await Task.Delay(config.PoliteDelayMs, cancellationToken);

            // Use Sri Lanka time (UTC+5:30) for date calculations
            var slTime = DateTime.UtcNow.AddHours(5.5);
            var todayStr = slTime.ToString("yyyy-MM-dd");
            var weekAgoStr = slTime.AddDays(-7).ToString("yyyy-MM-dd");

            // Build form data for POST request
            var formData = new List<KeyValuePair<string, string>>
            {
                new("lookupPage", "lookup_daily_exchange_rates.php"),
                new("startRange", "2006-11-11"),
                new("rangeType", "dates"),
                new("txtStart", weekAgoStr),
                new("txtEnd", todayStr),
                new("submit_button", "Submit")
            };

            foreach (var curValue in CurrencyFormValues.Values)
            {
                formData.Add(new("chk_cur[]", curValue));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, config.CbslUrl);
            request.Content = new FormUrlEncodedContent(formData);

            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return await ParseHtmlAsync(html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch CBSL rates");
            return [];
        }
    }

    internal async Task<IReadOnlyList<ExchangeRate>> ParseHtmlAsync(string html)
    {
        var rates = new List<ExchangeRate>();

        var browsingContext = BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = await browsingContext.OpenAsync(req => req.Content(html));

        // CBSL response has one <h2> per currency followed by a <div class="table-responsive"><table>
        var headings = document.QuerySelectorAll("h2");

        foreach (var h2 in headings)
        {
            var currencyName = h2.TextContent.Trim();
            var currencyCode = MapCurrencyNameToCode(currencyName);
            if (currencyCode is null) continue;

            // Find the next table after this heading (may be nested inside a div)
            var sibling = h2.NextElementSibling;
            AngleSharp.Dom.IElement? table = null;

            while (sibling is not null)
            {
                if (string.Equals(sibling.TagName, "TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    table = sibling;
                    break;
                }

                // Table may be inside a <div class="table-responsive">
                table = sibling.QuerySelector("table");
                if (table is not null) break;

                // Stop if we hit the next heading
                if (string.Equals(sibling.TagName, "H2", StringComparison.OrdinalIgnoreCase))
                    break;

                sibling = sibling.NextElementSibling;
            }

            if (table is null) continue;

            // Parse rows to find the most recent date's data
            var rows = table.QuerySelectorAll("tbody tr");
            DateOnly? latestDate = null;
            decimal? latestBuy = null;
            decimal? latestSell = null;

            foreach (var row in rows)
            {
                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 3) continue;

                var dateStr = cells[0].TextContent.Trim();
                if (!DateOnly.TryParse(dateStr, out var date)) continue;

                if (TryParseDecimal(cells[1].TextContent, out var buy) &&
                    TryParseDecimal(cells[2].TextContent, out var sell))
                {
                    if (latestDate is null || date > latestDate)
                    {
                        latestDate = date;
                        latestBuy = buy;
                        latestSell = sell;
                    }
                }
            }

            if (latestDate.HasValue && latestBuy.HasValue && latestSell.HasValue)
            {
                rates.Add(new ExchangeRate
                {
                    BaseCurrency = "LKR",
                    TargetCurrency = currencyCode,
                    BuyingRate = latestBuy.Value,
                    SellingRate = latestSell.Value,
                    Source = SourceName,
                    CollectedAt = DateTime.UtcNow,
                    RateDate = latestDate.Value
                });
            }
        }

        logger.LogInformation("CBSL: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static string? MapCurrencyNameToCode(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        return n switch
        {
            _ when n.Contains("united states") || n.Contains("us dollar") => "USD",
            _ when n.Contains("euro") => "EUR",
            _ when n.Contains("british") || n.Contains("sterling") => "GBP",
            _ when n.Contains("swiss") => "CHF",
            _ when n.Contains("yen") || n.Contains("japanese") => "JPY",
            _ when n.Contains("australian") => "AUD",
            _ when n.Contains("canadian") => "CAD",
            _ when n.Contains("singapore") => "SGD",
            _ when n.Contains("renminbi") || n.Contains("chinese") || n.Contains("yuan") => "CNY",
            _ => null
        };
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        var cleaned = text.Trim().Replace(",", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
    }
}
