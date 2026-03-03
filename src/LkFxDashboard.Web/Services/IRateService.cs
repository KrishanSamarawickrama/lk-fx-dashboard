using LkFxDashboard.Core.Models;

namespace LkFxDashboard.Web.Services;

public interface IRateService
{
    Task<IReadOnlyList<ExchangeRate>> GetLatestRatesAsync(
        string currency, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRate>> GetHistoryAsync(
        string currency, int days = 30, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(
        CancellationToken cancellationToken = default);

    Task<int> RefreshRatesAsync(CancellationToken cancellationToken = default);
}
