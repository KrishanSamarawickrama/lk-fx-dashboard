using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class ComBankScraperTests
{
    [Fact]
    public void SourceName_IsCorrect()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ComBankScraper>.Instance;
        var scraper = new ComBankScraper(httpClient, options, logger);

        scraper.SourceName.Should().Be(ScraperSource.ComBank);
    }

    [Fact]
    public void SupportedCurrencies_NotEmpty()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ComBankScraper>.Instance;
        var scraper = new ComBankScraper(httpClient, options, logger);

        scraper.SupportedCurrencies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseHtmlAsync_ValidTable_ExtractsTtRates()
    {
        var html = """
            <html><body>
            <table>
                <thead>
                    <tr><th>Currency</th><th>Cheques Buy</th><th>Cheques Sell</th><th>TT Buy</th><th>TT Sell</th><th>Cash Buy</th><th>Cash Sell</th></tr>
                </thead>
                <tbody>
                    <tr><td>US DOLLARS</td><td>303.96</td><td>312.35</td><td>303.93</td><td>312.35</td><td>305.85</td><td>312.35</td></tr>
                    <tr><td>EURO</td><td>320.50</td><td>340.10</td><td>320.45</td><td>340.15</td><td>321.00</td><td>340.20</td></tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ComBankScraper>.Instance;
        var scraper = new ComBankScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(2);

        var usd = rates.First(r => r.TargetCurrency == "USD");
        usd.BuyingRate.Should().Be(303.93m);
        usd.SellingRate.Should().Be(312.35m);
        usd.Source.Should().Be("Commercial Bank");
        usd.BaseCurrency.Should().Be("LKR");

        var eur = rates.First(r => r.TargetCurrency == "EUR");
        eur.BuyingRate.Should().Be(320.45m);
        eur.SellingRate.Should().Be(340.15m);
    }

    [Fact]
    public async Task ParseHtmlAsync_CommaFormattedRates_ParsesCorrectly()
    {
        var html = """
            <html><body>
            <table>
                <tbody>
                    <tr><td>JAPANESE YEN</td><td>1.95</td><td>2.15</td><td>1,018.59</td><td>1,025.30</td><td>1.96</td><td>2.16</td></tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ComBankScraper>.Instance;
        var scraper = new ComBankScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("JPY");
        rates[0].BuyingRate.Should().Be(1018.59m);
    }

    [Fact]
    public async Task ParseHtmlAsync_EmptyTable_ReturnsEmpty()
    {
        var html = "<html><body><table><tbody></tbody></table></body></html>";

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ComBankScraper>.Instance;
        var scraper = new ComBankScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHtmlAsync_UnknownCurrency_IsSkipped()
    {
        var html = """
            <html><body>
            <table>
                <tbody>
                    <tr><td>MARTIAN CREDITS</td><td>1.00</td><td>2.00</td><td>1.50</td><td>2.50</td><td>1.60</td><td>2.60</td></tr>
                    <tr><td>US DOLLARS</td><td>303.96</td><td>312.35</td><td>303.93</td><td>312.35</td><td>305.85</td><td>312.35</td></tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ComBankScraper>.Instance;
        var scraper = new ComBankScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("USD");
    }
}
