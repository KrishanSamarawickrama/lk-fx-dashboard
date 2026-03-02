using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class ScStandardCharteredScraperTests
{
    private static byte[] LoadPdfFixture()
    {
        var assembly = typeof(ScStandardCharteredScraperTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "LkFxDashboard.Tests.Fixtures.standard-chartered-rates.pdf")!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static ScStandardCharteredScraper CreateScraper(byte[] pdfBytes)
    {
        var httpClient = MockHttpMessageHandlerForBytes.CreateClient(pdfBytes);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ScStandardCharteredScraper>.Instance;
        return new ScStandardCharteredScraper(httpClient, options, logger);
    }

    [Fact]
    public void ParsePdf_ReturnsNonEmptyResults()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = scraper.ParsePdf(pdfBytes);

        rates.Should().NotBeEmpty();
    }

    [Fact]
    public void ParsePdf_Returns17Currencies()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = scraper.ParsePdf(pdfBytes);

        rates.Should().HaveCount(17);
    }

    [Fact]
    public void ParsePdf_CorrectSource()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = scraper.ParsePdf(pdfBytes);

        rates.Should().AllSatisfy(r =>
            r.Source.Should().Be(ScraperSource.StandardChartered));
    }

    [Fact]
    public void ParsePdf_PositiveBuyingRates()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = scraper.ParsePdf(pdfBytes);

        rates.Should().AllSatisfy(r =>
            r.BuyingRate.Should().BeGreaterThan(0));
    }

    [Fact]
    public void ParsePdf_SellingRateGreaterOrEqualBuyingRate()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = scraper.ParsePdf(pdfBytes);

        rates.Should().AllSatisfy(r =>
            r.SellingRate.Should().BeGreaterThanOrEqualTo(r.BuyingRate));
    }

    [Fact]
    public void ParsePdf_ContainsUsd()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = scraper.ParsePdf(pdfBytes);

        rates.Should().Contain(r => r.TargetCurrency == "USD");
    }

    [Fact]
    public async Task FetchRatesAsync_WithPdfBytes_ReturnsRates()
    {
        var pdfBytes = LoadPdfFixture();
        var scraper = CreateScraper(pdfBytes);

        var rates = await scraper.FetchRatesAsync();

        rates.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FetchRatesAsync_OnError_ReturnsEmpty()
    {
        var httpClient = MockHttpMessageHandlerForBytes.CreateClient(
            [], System.Net.HttpStatusCode.InternalServerError);
        var options = Options.Create(new ScraperOptions());
        var logger = NullLogger<ScStandardCharteredScraper>.Instance;
        var scraper = new ScStandardCharteredScraper(httpClient, options, logger);

        var rates = await scraper.FetchRatesAsync();

        rates.Should().BeEmpty();
    }

    [Fact]
    public void SourceName_IsCorrect()
    {
        var scraper = CreateScraper([]);
        scraper.SourceName.Should().Be(ScraperSource.StandardChartered);
    }
}
