using LkFxDashboard.Core.Models;

namespace LkFxDashboard.Core.Interfaces;

public interface IExchangeRateScraper
{
    string SourceName { get; }
    IReadOnlyList<string> SupportedCurrencies { get; }
    Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(CancellationToken cancellationToken = default);
}
