using System.Net;
using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class HnbScraperTests
{
    private static HnbScraper CreateScraper(string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var httpClient = MockHttpMessageHandler.CreateClient(json, statusCode, "application/json");
        var options = Options.Create(new ScraperOptions { PoliteDelayMs = 0 });
        var logger = NullLogger<HnbScraper>.Instance;
        return new HnbScraper(httpClient, options, logger);
    }

    [Fact]
    public void SourceName_IsCorrect()
    {
        var scraper = CreateScraper("[]");
        scraper.SourceName.Should().Be(ScraperSource.Hnb);
    }

    [Fact]
    public void SupportedCurrencies_NotEmpty()
    {
        var scraper = CreateScraper("[]");
        scraper.SupportedCurrencies.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseJson_ValidResponse_ExtractsRates()
    {
        var json = """
            [
                {"currency":"US Dollars","currencyCode":"USD","buyingRate":308.3,"sellingRate":314.3},
                {"currency":"Euro","currencyCode":"EUR","buyingRate":351.8969,"sellingRate":362.7903},
                {"currency":"British Pounds","currencyCode":"GBP","buyingRate":407.8866,"sellingRate":419.4111}
            ]
            """;

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().HaveCount(3);

        var usd = rates.First(r => r.TargetCurrency == "USD");
        usd.BuyingRate.Should().Be(308.3m);
        usd.SellingRate.Should().Be(314.3m);
        usd.Source.Should().Be("HNB");
        usd.BaseCurrency.Should().Be("LKR");
    }

    [Fact]
    public void ParseJson_EmptyArray_ReturnsEmpty()
    {
        var scraper = CreateScraper("[]");
        var rates = scraper.ParseJson("[]");

        rates.Should().BeEmpty();
    }

    [Fact]
    public void ParseJson_UnknownCurrency_IsSkipped()
    {
        var json = """
            [
                {"currency":"Unknown","currencyCode":"XYZ","buyingRate":10.0,"sellingRate":12.0},
                {"currency":"US Dollars","currencyCode":"USD","buyingRate":308.3,"sellingRate":314.3}
            ]
            """;

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("USD");
    }

    [Fact]
    public void ParseJson_ZeroRate_IsSkipped()
    {
        var json = """
            [
                {"currency":"US Dollars","currencyCode":"USD","buyingRate":0,"sellingRate":314.3},
                {"currency":"Euro","currencyCode":"EUR","buyingRate":351.8969,"sellingRate":362.7903}
            ]
            """;

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().HaveCount(1);
        rates[0].TargetCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task FetchRatesAsync_HttpError_ReturnsEmpty()
    {
        var scraper = CreateScraper("", HttpStatusCode.InternalServerError);
        var rates = await scraper.FetchRatesAsync();

        rates.Should().BeEmpty();
    }

    [Fact]
    public void ParseJson_FixtureData_ReturnsAll17Currencies()
    {
        var assembly = typeof(HnbScraperTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "LkFxDashboard.Tests.Fixtures.hnb-rates.json")!;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().HaveCount(17);
        rates.Should().AllSatisfy(r => r.Source.Should().Be("HNB"));
        rates.Should().AllSatisfy(r => r.BuyingRate.Should().BeGreaterThan(0));
        rates.Should().AllSatisfy(r => r.SellingRate.Should().BeGreaterThanOrEqualTo(r.BuyingRate));
    }
}
