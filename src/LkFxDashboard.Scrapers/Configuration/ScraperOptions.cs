namespace LkFxDashboard.Scrapers.Configuration;

public class ScraperOptions
{
    public const string SectionName = "Scraping";

    /// <summary>
    /// Standard Chartered LK exchange rates PDF.
    /// </summary>
    public string StandardCharteredUrl { get; set; } =
        "https://av.sc.com/lk/content/docs/lk-exchange-rates.pdf";

    /// <summary>
    /// CBSL TT buy/sell rates POST endpoint (returns HTML tables).
    /// </summary>
    public string CbslUrl { get; set; } =
        "https://www.cbsl.gov.lk/cbsl_custom/exratestt/exrates_resultstt.php";

    /// <summary>
    /// OANDA currency converter page.
    /// </summary>
    public string OandaUrl { get; set; } =
        "https://www.oanda.com/currency-converter/en/?from=USD&to=LKR&amount=1";

    /// <summary>
    /// Whether to use PuppeteerSharp for OANDA (required — plain HTTP does not work).
    /// </summary>
    public bool OandaUsePuppeteer { get; set; } = true;

    /// <summary>
    /// Whether OANDA scraping is enabled (requires Chromium download on first run).
    /// </summary>
    public bool OandaEnabled { get; set; } = false;

    /// <summary>
    /// HSBC LK foreign exchange rates PDF.
    /// </summary>
    public string HsbcPdfUrl { get; set; } =
        "https://www.hsbc.lk/content/dam/hsbc/lk/documents/tariffs/foreign-exchange-rates.pdf";

    public int TimeoutSeconds { get; set; } = 30;
    public int PoliteDelayMs { get; set; } = 2000;
}
