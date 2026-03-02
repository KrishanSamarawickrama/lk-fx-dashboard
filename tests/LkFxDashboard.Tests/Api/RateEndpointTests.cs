using FluentAssertions;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using System.Net;

namespace LkFxDashboard.Tests.Api;

public class RateEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestApiKey = "test-api-key-for-integration";
    private const string TestAdminPin = "9999";

    private readonly WebApplicationFactory<Program> _factory;

    public RateEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Security:ApiKey", TestApiKey);
            builder.UseSetting("Security:AdminPin", TestAdminPin);

            builder.ConfigureServices(services =>
            {
                // Remove ALL DbContext, EF Core, and Npgsql/Aspire-related registrations
                var toRemove = services
                    .Where(d =>
                    {
                        var typeName = d.ServiceType.FullName ?? "";
                        var implName = d.ImplementationType?.FullName ?? "";
                        return typeName.Contains("AppDbContext")
                            || typeName.Contains("Npgsql")
                            || typeName.Contains("DbContextOptions")
                            || typeName.Contains("DbContextPool")
                            || typeName.Contains("DbContextFactory")
                            || typeName.Contains("DbContextLease")
                            || typeName.Contains("DbContextOptionsConfiguration")
                            || implName.Contains("AppDbContext")
                            || implName.Contains("Npgsql");
                    })
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                // Add in-memory database + factory
                var dbName = "TestDb_" + Guid.NewGuid();
                services.AddDbContextFactory<AppDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // Ensure health checks are registered
                services.AddHealthChecks();

                // Remove real scrapers and add mock
                services.RemoveAll<IExchangeRateScraper>();

                var mockScraper = Substitute.For<IExchangeRateScraper>();
                mockScraper.SourceName.Returns("MockSource");
                mockScraper.FetchRatesAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<ExchangeRate>
                    {
                        new()
                        {
                            TargetCurrency = "USD",
                            BuyingRate = 300m,
                            SellingRate = 305m,
                            Source = "MockSource",
                            RateDate = DateOnly.FromDateTime(DateTime.UtcNow)
                        }
                    });
                services.AddSingleton<IExchangeRateScraper>(mockScraper);

                // Remove background scraping service
                var hostedServices = services
                    .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                        && d.ImplementationType?.Name == "RateScrapingBackgroundService")
                    .ToList();
                foreach (var d in hostedServices)
                    services.Remove(d);
            });
        });
    }

    [Fact]
    public async Task GetLatestRates_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/rates/latest?currency=USD");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHistoryRates_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/rates/history?currency=USD&days=30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCurrencies_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/currencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TriggerScrape_WithValidApiKey_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/scrape/trigger");
        request.Headers.Add("X-Api-Key", TestApiKey);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TriggerScrape_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/scrape/trigger", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TriggerScrape_WithWrongApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/scrape/trigger");
        request.Headers.Add("X-Api-Key", "wrong-key");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TriggerScrape_ExceedingRateLimit_Returns429()
    {
        var client = _factory.CreateClient();

        // Send 2 requests (within the limit)
        for (var i = 0; i < 2; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/scrape/trigger");
            req.Headers.Add("X-Api-Key", TestApiKey);
            await client.SendAsync(req);
        }

        // Third request should be rate-limited
        var thirdRequest = new HttpRequestMessage(HttpMethod.Post, "/api/scrape/trigger");
        thirdRequest.Headers.Add("X-Api-Key", TestApiKey);
        var response = await client.SendAsync(thirdRequest);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
