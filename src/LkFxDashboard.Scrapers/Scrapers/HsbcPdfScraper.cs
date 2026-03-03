using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Scrapes exchange rates from HSBC Sri Lanka's PDF.
/// Uses X-coordinate column detection to correctly extract TT Buy/Sell values,
/// because PdfPig splits some numbers across word boundaries in this PDF.
/// </summary>
public class HsbcPdfScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<HsbcPdfScraper> logger) : IExchangeRateScraper
{
    private static readonly HashSet<string> KnownCurrencies =
    [
        "USD", "EUR", "GBP", "CHF", "JPY", "AUD", "CAD", "SGD",
        "AED", "BHD", "CNY", "DKK", "HKD", "INR", "KWD", "NOK",
        "NZD", "SAR", "SEK", "THB"
    ];

    // Column X boundaries determined from header word positions in the PDF
    // CCY Code column: X ≈ 180-240
    // TT Buy column:   X ≈ 240-320  (data starts at X=271 for large, X=277/282 for small values)
    // TT Sell column:  X ≈ 320-400  (data starts at X=341 for large, X=346/351 for small values)
    private const double CodeMinX = 180;
    private const double CodeMaxX = 240;
    private const double TtBuyMinX = 240;
    private const double TtBuyMaxX = 320;
    private const double TtSellMinX = 320;
    private const double TtSellMaxX = 400;

    // Y-coordinate tolerance for grouping words into the same row
    private const double YTolerance = 3.0;

    public string SourceName => ScraperSource.Hsbc;

    public IReadOnlyList<string> SupportedCurrencies => KnownCurrencies.ToList();

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching HSBC PDF: {Url}", config.HsbcPdfUrl);

            var pdfBytes = await httpClient.GetByteArrayAsync(config.HsbcPdfUrl, cancellationToken);

            return ParsePdf(pdfBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch HSBC PDF rates");
            return [];
        }
    }

    internal IReadOnlyList<ExchangeRate> ParsePdf(byte[] pdfBytes)
    {
        var rates = new List<ExchangeRate>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var document = PdfDocument.Open(pdfBytes);
        if (document.NumberOfPages < 1) return rates;
        var page = document.GetPage(1);

        var words = page.GetWords().ToList();

        // Extract the rate date from "LAST RATE CHANGE EFFECTED ON dd-MMM-yy"
        var allText = string.Join(" ", words.Select(w => w.Text));
        var rateDate = ExtractDate(allText) ?? today;

        // Group words into rows by Y-coordinate with tolerance
        var rows = GroupWordsIntoRows(words);

        foreach (var row in rows)
        {
            // Find a 3-letter currency code in the CCY Code column (X: 180-240)
            var codeWord = row.FirstOrDefault(w =>
                w.BoundingBox.Left >= CodeMinX && w.BoundingBox.Left < CodeMaxX
                && w.Text.Trim().Length == 3
                && KnownCurrencies.Contains(w.Text.Trim().ToUpperInvariant()));

            if (codeWord is null) continue;

            var currency = codeWord.Text.Trim().ToUpperInvariant();

            // Concatenate all word fragments in the TT Buy column (X: 240-320)
            var ttBuyStr = ConcatenateWordsInXRange(row, TtBuyMinX, TtBuyMaxX);

            // Concatenate all word fragments in the TT Sell column (X: 320-400)
            var ttSellStr = ConcatenateWordsInXRange(row, TtSellMinX, TtSellMaxX);

            var ttBuy = TryParseDecimal(ttBuyStr);
            var ttSell = TryParseDecimal(ttSellStr);

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
                    RateDate = rateDate
                });
            }
        }

        logger.LogInformation("HSBC PDF: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static List<List<Word>> GroupWordsIntoRows(List<Word> words)
    {
        var sorted = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();
        var rows = new List<List<Word>>();
        var currentRow = new List<Word>();

        foreach (var word in sorted)
        {
            var y = word.BoundingBox.Bottom;
            if (currentRow.Count == 0 ||
                Math.Abs(y - currentRow[0].BoundingBox.Bottom) <= YTolerance)
            {
                currentRow.Add(word);
            }
            else
            {
                rows.Add(currentRow);
                currentRow = [word];
            }
        }

        if (currentRow.Count > 0) rows.Add(currentRow);
        return rows;
    }

    private static string ConcatenateWordsInXRange(List<Word> row, double minX, double maxX)
    {
        return string.Join("", row
            .Where(w => w.BoundingBox.Left >= minX && w.BoundingBox.Left < maxX)
            .OrderBy(w => w.BoundingBox.Left)
            .Select(w => w.Text.Trim()));
    }

    private static DateOnly? ExtractDate(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"EFFECTED\s+ON\s+(\d{1,2}-\w{3}-\d{2,4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var dateStr = match.Groups[1].Value;
            if (DateOnly.TryParseExact(dateStr, ["dd-MMM-yy", "dd-MMM-yyyy"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static decimal? TryParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase))
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
