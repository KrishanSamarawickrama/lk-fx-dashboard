using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class OandaScraperTests
{
    [Fact]
    public async Task FetchRatesAsync_WhenDisabled_ReturnsEmpty()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions { OandaEnabled = false });
        var logger = NullLogger<OandaScraper>.Instance;
        var scraper = new OandaScraper(httpClient, options, logger);

        var rates = await scraper.FetchRatesAsync();

        rates.Should().BeEmpty();
    }

    [Fact]
    public void SourceName_IsCorrect()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<OandaScraper>.Instance;
        var scraper = new OandaScraper(httpClient, options, logger);

        scraper.SourceName.Should().Be(ScraperSource.Oanda);
    }

    [Fact]
    public void SupportedCurrencies_NotEmpty()
    {
        var httpClient = MockHttpMessageHandler.CreateClient("<html></html>");
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<OandaScraper>.Instance;
        var scraper = new OandaScraper(httpClient, options, logger);

        scraper.SupportedCurrencies.Should().NotBeEmpty();
    }
}
