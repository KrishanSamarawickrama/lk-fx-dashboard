using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LkFxDashboard.Data.Repositories;

public class ExchangeRateRepository(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<ExchangeRateRepository> logger) : IExchangeRateRepository
{
    public async Task<IReadOnlyList<ExchangeRate>> GetLatestRatesAsync(
        string targetCurrency,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Get the latest rate per source — each source may have a different latest date
        // (e.g., SC uses today's date, CBSL uses latest weekday, HSBC uses last rate change date)
        return await db.ExchangeRates
            .Where(r => r.TargetCurrency == targetCurrency)
            .Where(r => r.RateDate == db.ExchangeRates
                .Where(r2 => r2.TargetCurrency == targetCurrency && r2.Source == r.Source)
                .Max(r2 => r2.RateDate))
            .OrderBy(r => r.Source)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetHistoryAsync(
        string targetCurrency,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

        return await db.ExchangeRates
            .Where(r => r.TargetCurrency == targetCurrency && r.RateDate >= cutoff)
            .OrderBy(r => r.RateDate)
            .ThenBy(r => r.Source)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<ExchangeRate> rates,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rateList = rates.ToList();
        if (rateList.Count == 0) return;

        logger.LogInformation("AddRangeAsync: saving {Count} rates to database", rateList.Count);

        // Remove existing rates for same source/currency/date to avoid duplicates
        var removedCount = 0;
        foreach (var group in rateList.GroupBy(r => new { r.Source, r.TargetCurrency, r.RateDate }))
        {
            var existing = await db.ExchangeRates
                .Where(r => r.Source == group.Key.Source
                    && r.TargetCurrency == group.Key.TargetCurrency
                    && r.RateDate == group.Key.RateDate)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
            {
                db.ExchangeRates.RemoveRange(existing);
                removedCount += existing.Count;
            }
        }

        db.ExchangeRates.AddRange(rateList);
        var saved = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "AddRangeAsync: completed. Removed {Removed}, added {Added}, SaveChanges affected {Saved} rows",
            removedCount, rateList.Count, saved);
    }

    public async Task<IReadOnlyList<string>> GetAvailableCurrenciesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.ExchangeRates
            .Select(r => r.TargetCurrency)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }
}
