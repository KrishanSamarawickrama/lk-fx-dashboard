using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes exchange rates from Standard Chartered Sri Lanka's PDF.
/// PDF contains 17 currencies with TT Buy/Sell, CR Buy/Sell, Draft Buy/Sell, and Import Bill columns.
/// We extract TT Buying Rate and TT Selling Rate.
/// </summary>
public class ScStandardCharteredScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<ScStandardCharteredScraper> logger) : IExchangeRateScraper
{
    private static readonly HashSet<string> KnownCurrencies =
    [
        "USD", "EUR", "GBP", "CHF", "JPY", "AUD", "CAD", "SGD", "INR",
        "AED", "CNY", "DKK", "HKD", "NOK", "NZD", "SEK", "ZAR"
    ];

    public string SourceName => ScraperSource.StandardChartered;

    public IReadOnlyList<string> SupportedCurrencies => KnownCurrencies.ToList();

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching SC PDF: {Url}", config.StandardCharteredUrl);

            var pdfBytes = await httpClient.GetByteArrayAsync(config.StandardCharteredUrl, cancellationToken);

            return ParsePdf(pdfBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Standard Chartered rates");
            return [];
        }
    }

    internal IReadOnlyList<ExchangeRate> ParsePdf(byte[] pdfBytes)
    {
        var rates = new List<ExchangeRate>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var document = PdfDocument.Open(pdfBytes);
        var page = document.GetPage(1);
        var words = page.GetWords().ToList();

        // Group words by Y coordinate (same row), with rounding tolerance
        var lines = words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
            .OrderByDescending(g => g.Key)
            .Select(g => g.OrderBy(w => w.BoundingBox.Left)
                .Select(w => w.Text.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList())
            .ToList();

        foreach (var line in lines)
        {
            // Find a 3-letter currency code token
            var codeIndex = line.FindIndex(w =>
                w.Length == 3 && KnownCurrencies.Contains(w.ToUpperInvariant()));
            if (codeIndex < 0) continue;

            var currency = line[codeIndex].ToUpperInvariant();

            // After the currency code, tokens are the rate columns in order:
            // TT Buy, TT Sell, CR Buy, CR Sell, Draft Buy, Draft Sell, Import Bill
            var tokensAfterCode = line.Skip(codeIndex + 1).ToList();
            if (tokensAfterCode.Count < 2) continue;

            // First two tokens are TT Buying Rate and TT Selling Rate
            var ttBuy = TryParseDecimal(tokensAfterCode[0]);
            var ttSell = TryParseDecimal(tokensAfterCode[1]);

            if (ttBuy.HasValue && ttSell.HasValue)
            {
                rates.Add(new ExchangeRate
                {
                    BaseCurrency = "LKR",
                    TargetCurrency = currency,
                    BuyingRate = ttBuy.Value,
                    SellingRate = ttSell.Value,
                    Source = SourceName,
                    CollectedAt = DateTime.UtcNow,
                    RateDate = today
                });
            }
        }

        logger.LogInformation("Standard Chartered PDF: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static decimal? TryParseDecimal(string text)
    {
        if (string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase))
            return null;

        var cleaned = text.Trim().Replace(",", "");
        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return value;
        }

        return null;
    }
}
