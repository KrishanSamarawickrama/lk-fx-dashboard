using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class BocScraperTests
{
    [Fact]
    public void SourceName_IsCorrect()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<BocScraper>.Instance;
        var scraper = new BocScraper(httpClient, options, logger);

        scraper.SourceName.Should().Be(ScraperSource.Boc);
    }

    [Fact]
    public void SupportedCurrencies_NotEmpty()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<BocScraper>.Instance;
        var scraper = new BocScraper(httpClient, options, logger);

        scraper.SupportedCurrencies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseHtmlAsync_ValidTable_ExtractsTtRates()
    {
        var html = """
            <html><body>
            <table>
                <thead>
                    <tr><th>Currency</th><th>Drafts Buy</th><th>Drafts Sell</th><th>TT Buy</th><th>TT Sell</th><th>Cash Buy</th><th>Cash Sell</th></tr>
                </thead>
                <tbody>
                    <tr><td>USD</td><td>307.8000</td><td>314.8000</td><td>307.5000</td><td>314.8000</td><td>307.8000</td><td>314.8000</td></tr>
                    <tr><td>EUR</td><td>330.1200</td><td>345.6700</td><td>329.9000</td><td>345.7000</td><td>330.5000</td><td>345.8000</td></tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<BocScraper>.Instance;
        var scraper = new BocScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(2);

        var usd = rates.First(r => r.TargetCurrency == "USD");
        usd.BuyingRate.Should().Be(307.5000m);
        usd.SellingRate.Should().Be(314.8000m);
        usd.Source.Should().Be("BOC");
        usd.BaseCurrency.Should().Be("LKR");

        var eur = rates.First(r => r.TargetCurrency == "EUR");
        eur.BuyingRate.Should().Be(329.9000m);
        eur.SellingRate.Should().Be(345.7000m);
    }

    [Fact]
    public async Task ParseHtmlAsync_FallsBackToDrafts_WhenTtUnavailable()
    {
        var html = """
            <html><body>
            <table>
                <tbody>
                    <tr><td>AED</td><td>76.9527</td><td>89.2337</td><td>-</td><td>-</td><td>-</td><td>-</td></tr>
                    <tr><td>USD</td><td>307.8000</td><td>314.8000</td><td>307.5000</td><td>314.8000</td><td>307.8000</td><td>314.8000</td></tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<BocScraper>.Instance;
        var scraper = new BocScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(2);

        var aed = rates.First(r => r.TargetCurrency == "AED");
        aed.BuyingRate.Should().Be(76.9527m);
        aed.SellingRate.Should().Be(89.2337m);

        var usd = rates.First(r => r.TargetCurrency == "USD");
        usd.BuyingRate.Should().Be(307.5000m);
    }

    [Fact]
    public async Task ParseHtmlAsync_EmptyTable_ReturnsEmpty()
    {
        var html = "<html><body><table><tbody></tbody></table></body></html>";

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<BocScraper>.Instance;
        var scraper = new BocScraper(httpClient, options, logger);

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
                    <tr><td>XYZ</td><td>1.00</td><td>2.00</td><td>1.50</td><td>2.50</td><td>1.60</td><td>2.60</td></tr>
                    <tr><td>USD</td><td>307.8000</td><td>314.8000</td><td>307.5000</td><td>314.8000</td><td>307.8000</td><td>314.8000</td></tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<BocScraper>.Instance;
        var scraper = new BocScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("USD");
    }
}
