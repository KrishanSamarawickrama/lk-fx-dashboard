using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class DfccScraperTests
{
    [Fact]
    public void SourceName_IsCorrect()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<DfccScraper>.Instance;
        var scraper = new DfccScraper(httpClient, options, logger);

        scraper.SourceName.Should().Be(ScraperSource.Dfcc);
    }

    [Fact]
    public void SupportedCurrencies_NotEmpty()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<DfccScraper>.Instance;
        var scraper = new DfccScraper(httpClient, options, logger);

        scraper.SupportedCurrencies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseHtmlAsync_ValidTable_ExtractsTtRates()
    {
        var html = """
            <html><body>
            <table class="table bg-light">
                <thead>
                    <tr>
                        <th>Currency</th>
                        <th>DD Buying</th>
                        <th>Currency Note Encashment</th>
                        <th>TT Buying</th>
                        <th>DD/TT Selling</th>
                        <th>Currency Note Selling</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td><img alt="USD" src="flags/usd.png" /> US Dollar</td>
                        <td>300.00</td>
                        <td>298.50</td>
                        <td>301.50</td>
                        <td>313.00</td>
                        <td>315.00</td>
                    </tr>
                    <tr>
                        <td><img alt="EUR" src="flags/eur.png" /> Euro</td>
                        <td>322.00</td>
                        <td>320.00</td>
                        <td>323.50</td>
                        <td>341.00</td>
                        <td>343.00</td>
                    </tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<DfccScraper>.Instance;
        var scraper = new DfccScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(2);

        var usd = rates.First(r => r.TargetCurrency == "USD");
        usd.BuyingRate.Should().Be(301.50m);
        usd.SellingRate.Should().Be(313.00m);
        usd.Source.Should().Be("DFCC");
        usd.BaseCurrency.Should().Be("LKR");

        var eur = rates.First(r => r.TargetCurrency == "EUR");
        eur.BuyingRate.Should().Be(323.50m);
        eur.SellingRate.Should().Be(341.00m);
    }

    [Fact]
    public async Task ParseHtmlAsync_DashRate_IsSkipped()
    {
        var html = """
            <html><body>
            <table class="table bg-light">
                <tbody>
                    <tr>
                        <td><img alt="USD" src="flags/usd.png" /> US Dollar</td>
                        <td>300.00</td>
                        <td>298.50</td>
                        <td>-</td>
                        <td>313.00</td>
                        <td>315.00</td>
                    </tr>
                    <tr>
                        <td><img alt="GBP" src="flags/gbp.png" /> Pound Sterling</td>
                        <td>380.00</td>
                        <td>378.00</td>
                        <td>381.00</td>
                        <td>395.00</td>
                        <td>397.00</td>
                    </tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<DfccScraper>.Instance;
        var scraper = new DfccScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        // USD row has "-" for TT Buying → skipped; only GBP returned
        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("GBP");
    }

    [Fact]
    public async Task ParseHtmlAsync_EmptyTable_ReturnsEmpty()
    {
        var html = """
            <html><body>
            <table class="table bg-light"><tbody></tbody></table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<DfccScraper>.Instance;
        var scraper = new DfccScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHtmlAsync_UnknownCurrencyCode_IsSkipped()
    {
        var html = """
            <html><body>
            <table class="table bg-light">
                <tbody>
                    <tr>
                        <td><img alt="XYZ" src="flags/xyz.png" /> Unknown</td>
                        <td>10.00</td>
                        <td>9.50</td>
                        <td>10.50</td>
                        <td>12.00</td>
                        <td>12.50</td>
                    </tr>
                    <tr>
                        <td><img alt="USD" src="flags/usd.png" /> US Dollar</td>
                        <td>300.00</td>
                        <td>298.50</td>
                        <td>301.50</td>
                        <td>313.00</td>
                        <td>315.00</td>
                    </tr>
                </tbody>
            </table>
            </body></html>
            """;

        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<DfccScraper>.Instance;
        var scraper = new DfccScraper(httpClient, options, logger);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("USD");
    }
}
