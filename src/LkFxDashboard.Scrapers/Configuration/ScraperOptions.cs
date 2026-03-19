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
    /// Commercial Bank of Ceylon exchange rates page.
    /// </summary>
    public string ComBankUrl { get; set; } =
        "https://www.combank.lk/rates-tariff#exchange-rates";

    /// <summary>
    /// HSBC LK foreign exchange rates PDF.
    /// </summary>
    public string HsbcPdfUrl { get; set; } =
        "https://www.hsbc.lk/content/dam/hsbc/lk/documents/tariffs/foreign-exchange-rates.pdf";

    /// <summary>
    /// DFCC Bank exchange rates page.
    /// </summary>
    public string DfccUrl { get; set; } =
        "https://www.dfcc.lk/rates-and-tariff/exchange-rates";

    /// <summary>
    /// HNB exchange rates JSON API.
    /// </summary>
    public string HnbUrl { get; set; } =
        "https://venus.hnb.lk/api/get_exchange_rates_contents_web";

    public int TimeoutSeconds { get; set; } = 30;
    public int PoliteDelayMs { get; set; } = 2000;
}
