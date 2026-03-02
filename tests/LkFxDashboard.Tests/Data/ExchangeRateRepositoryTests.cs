using FluentAssertions;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Data;
using LkFxDashboard.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LkFxDashboard.Tests.Data;

public class ExchangeRateRepositoryTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TestDbContextFactory _factory;
    private readonly ExchangeRateRepository _repository;

    public ExchangeRateRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _factory = new TestDbContextFactory(_options);
        _repository = new ExchangeRateRepository(_factory, NullLogger<ExchangeRateRepository>.Instance);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private AppDbContext CreateContext() => new(_options);

    [Fact]
    public async Task AddRangeAsync_AddsRates()
    {
        var rates = CreateSampleRates(DateOnly.FromDateTime(DateTime.UtcNow));

        await _repository.AddRangeAsync(rates);

        await using var db = CreateContext();
        db.ExchangeRates.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLatestRatesAsync_ReturnsLatestDate()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await _repository.AddRangeAsync(CreateSampleRates(yesterday));
        await _repository.AddRangeAsync(CreateSampleRates(today));

        var latest = await _repository.GetLatestRatesAsync("USD");

        latest.Should().AllSatisfy(r => r.RateDate.Should().Be(today));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsRatesWithinDateRange()
    {
        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60));
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

        await _repository.AddRangeAsync(CreateSampleRates(oldDate));
        await _repository.AddRangeAsync(CreateSampleRates(recentDate));

        var history = await _repository.GetHistoryAsync("USD", 30);

        history.Should().AllSatisfy(r =>
            r.RateDate.Should().BeOnOrAfter(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))));
    }

    [Fact]
    public async Task GetAvailableCurrenciesAsync_ReturnsDistinctCurrencies()
    {
        await _repository.AddRangeAsync(
        [
            new ExchangeRate
            {
                TargetCurrency = "USD", Source = "Test",
                BuyingRate = 300, SellingRate = 305,
                RateDate = DateOnly.FromDateTime(DateTime.UtcNow)
            },
            new ExchangeRate
            {
                TargetCurrency = "EUR", Source = "Test",
                BuyingRate = 320, SellingRate = 330,
                RateDate = DateOnly.FromDateTime(DateTime.UtcNow)
            },
            new ExchangeRate
            {
                TargetCurrency = "USD", Source = "Test2",
                BuyingRate = 301, SellingRate = 306,
                RateDate = DateOnly.FromDateTime(DateTime.UtcNow)
            }
        ]);

        var currencies = await _repository.GetAvailableCurrenciesAsync();

        currencies.Should().HaveCount(2);
        currencies.Should().Contain("USD");
        currencies.Should().Contain("EUR");
    }

    [Fact]
    public async Task AddRangeAsync_ReplacesExistingRates()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await _repository.AddRangeAsync(
        [
            new ExchangeRate
            {
                TargetCurrency = "USD", Source = "TestSource",
                BuyingRate = 300, SellingRate = 305, RateDate = today
            }
        ]);

        await _repository.AddRangeAsync(
        [
            new ExchangeRate
            {
                TargetCurrency = "USD", Source = "TestSource",
                BuyingRate = 310, SellingRate = 315, RateDate = today
            }
        ]);

        var rates = await _repository.GetLatestRatesAsync("USD");
        rates.Should().HaveCount(1);
        rates[0].BuyingRate.Should().Be(310);
    }

    private static List<ExchangeRate> CreateSampleRates(DateOnly date) =>
    [
        new ExchangeRate
        {
            TargetCurrency = "USD",
            Source = ScraperSource.StandardChartered,
            BuyingRate = 295.50m,
            SellingRate = 305.25m,
            RateDate = date,
            CollectedAt = DateTime.UtcNow
        },
        new ExchangeRate
        {
            TargetCurrency = "USD",
            Source = ScraperSource.Cbsl,
            BuyingRate = 294.80m,
            SellingRate = 306.10m,
            RateDate = date,
            CollectedAt = DateTime.UtcNow
        }
    ];

    private class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
