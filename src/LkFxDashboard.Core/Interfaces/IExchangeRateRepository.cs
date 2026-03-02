using LkFxDashboard.Core.Models;

namespace LkFxDashboard.Core.Interfaces;

public interface IExchangeRateRepository
{
    Task<IReadOnlyList<ExchangeRate>> GetLatestRatesAsync(
        string targetCurrency,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRate>> GetHistoryAsync(
        string targetCurrency,
        int days = 30,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<ExchangeRate> rates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableCurrenciesAsync(
        CancellationToken cancellationToken = default);
}
