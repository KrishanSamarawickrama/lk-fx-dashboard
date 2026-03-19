using System.Net;
using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using LkFxDashboard.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Tests.Scrapers;

public class SampathScraperTests
{
    private static SampathScraper CreateScraper(string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var httpClient = MockHttpMessageHandler.CreateClient(json, statusCode, "application/json");
        var options = Options.Create(new ScraperOptions { PoliteDelayMs = 0 });
        var logger = NullLogger<SampathScraper>.Instance;
        return new SampathScraper(httpClient, options, logger);
    }

    [Fact]
    public void SourceName_IsCorrect()
    {
        var scraper = CreateScraper("""{"success":true,"data":[]}""");
        scraper.SourceName.Should().Be(ScraperSource.Sampath);
    }

    [Fact]
    public void SupportedCurrencies_NotEmpty()
    {
        var scraper = CreateScraper("""{"success":true,"data":[]}""");
        scraper.SupportedCurrencies.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseJson_ValidResponse_ExtractsRates()
    {
        var json = """
            {
                "success": true,
                "data": [
                    {"CurrCode":"USD","CurrName":"U.S. Dollar","TTBUY":"308.25","TTSEL":"314.75","ODBUY":"306.45"},
                    {"CurrCode":"EUR","CurrName":"Euro","TTBUY":"351.9466","TTSEL":"363.7916","ODBUY":"351.15"},
                    {"CurrCode":"GBP","CurrName":"U.K. Pound","TTBUY":"408.3878","TTSEL":"420.2468","ODBUY":"407.47"}
                ]
            }
            """;

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().HaveCount(3);

        var usd = rates.First(r => r.TargetCurrency == "USD");
        usd.BuyingRate.Should().Be(308.25m);
        usd.SellingRate.Should().Be(314.75m);
        usd.Source.Should().Be("Sampath");
        usd.BaseCurrency.Should().Be("LKR");
    }

    [Fact]
    public void ParseJson_EmptyData_ReturnsEmpty()
    {
        var json = """{"success":true,"data":[]}""";

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().BeEmpty();
    }

    [Fact]
    public void ParseJson_UnknownCurrency_IsSkipped()
    {
        var json = """
            {
                "success": true,
                "data": [
                    {"CurrCode":"XYZ","CurrName":"Unknown","TTBUY":"10.0","TTSEL":"12.0","ODBUY":"9.5"},
                    {"CurrCode":"USD","CurrName":"U.S. Dollar","TTBUY":"308.25","TTSEL":"314.75","ODBUY":"306.45"}
                ]
            }
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
            {
                "success": true,
                "data": [
                    {"CurrCode":"USD","CurrName":"U.S. Dollar","TTBUY":"0","TTSEL":"314.75","ODBUY":"306.45"},
                    {"CurrCode":"EUR","CurrName":"Euro","TTBUY":"351.9466","TTSEL":"363.7916","ODBUY":"351.15"}
                ]
            }
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
        var assembly = typeof(SampathScraperTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "LkFxDashboard.Tests.Fixtures.sampath-rates.json")!;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var scraper = CreateScraper(json);
        var rates = scraper.ParseJson(json);

        rates.Should().HaveCount(17);
        rates.Should().AllSatisfy(r => r.Source.Should().Be("Sampath"));
        rates.Should().AllSatisfy(r => r.BuyingRate.Should().BeGreaterThan(0));
        rates.Should().AllSatisfy(r => r.SellingRate.Should().BeGreaterThanOrEqualTo(r.BuyingRate));
    }
}
