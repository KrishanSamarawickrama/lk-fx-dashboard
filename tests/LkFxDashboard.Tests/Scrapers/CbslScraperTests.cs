using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class CbslScraperTests
{
    private static string LoadFixture()
    {
        var assembly = typeof(CbslScraperTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "LkFxDashboard.Tests.Fixtures.cbsl-response.html")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static CbslScraper CreateScraper(string html)
    {
        var httpClient = MockHttpMessageHandler.CreateClient(html);
        var options = Options.Create(new ScraperOptions { PoliteDelayMs = 0 });
        var logger = NullLogger<CbslScraper>.Instance;
        return new CbslScraper(httpClient, options, logger);
    }

    [Fact]
    public async Task ParseHtml_ReturnsNonEmptyResults()
    {
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseHtml_Returns3Currencies()
    {
        // Fixture has USD, EUR, GBP
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().HaveCount(3);
    }

    [Fact]
    public async Task ParseHtml_CorrectSource()
    {
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().AllSatisfy(r =>
            r.Source.Should().Be(ScraperSource.Cbsl));
    }

    [Fact]
    public async Task ParseHtml_PositiveBuyingRates()
    {
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().AllSatisfy(r =>
            r.BuyingRate.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ParseHtml_SellingRateGreaterOrEqualBuyingRate()
    {
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().AllSatisfy(r =>
            r.SellingRate.Should().BeGreaterThanOrEqualTo(r.BuyingRate));
    }

    [Fact]
    public async Task ParseHtml_TakesLatestDate()
    {
        // Fixture has data from 2026-02-24 to 2026-02-27; should pick 2026-02-27
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().AllSatisfy(r =>
            r.RateDate.Should().Be(new DateOnly(2026, 2, 27)));
    }

    [Fact]
    public async Task ParseHtml_ContainsUsd()
    {
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.ParseHtmlAsync(html);

        rates.Should().Contain(r => r.TargetCurrency == "USD");
    }

    [Fact]
    public async Task FetchRatesAsync_WithHtml_ReturnsRates()
    {
        var html = LoadFixture();
        var scraper = CreateScraper(html);

        var rates = await scraper.FetchRatesAsync();

        rates.Should().NotBeEmpty();
    }

    [Fact]
    public void SourceName_IsCorrect()
    {
        var scraper = CreateScraper("<html></html>");
        scraper.SourceName.Should().Be(ScraperSource.Cbsl);
    }
}
