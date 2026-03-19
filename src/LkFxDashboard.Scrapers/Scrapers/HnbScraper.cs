using System.Text.Json;
using System.Text.Json.Serialization;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Fetches exchange rates from HNB's JSON API.
/// No HTML/PDF parsing required — the endpoint returns a JSON array of rate objects.
/// </summary>
public class HnbScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<HnbScraper> logger) : IExchangeRateScraper
{
    public string SourceName => ScraperSource.Hnb;

    public IReadOnlyList<string> SupportedCurrencies =>
        ["USD", "EUR", "GBP", "JPY", "CHF", "AUD", "CAD", "SGD", "HKD", "INR",
         "CNY", "DKK", "NZD", "NOK", "SEK", "THB", "AED"];

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching rates from HNB: {Url}", config.HnbUrl);

            await Task.Delay(config.PoliteDelayMs, cancellationToken);

            var response = await httpClient.GetAsync(config.HnbUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            return ParseJson(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch HNB rates");
            return [];
        }
    }

    internal IReadOnlyList<ExchangeRate> ParseJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<HnbRateDto>>(json, JsonOptions);
        if (dtos is null || dtos.Count == 0)
            return [];

        var supported = new HashSet<string>(SupportedCurrencies, StringComparer.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rates = new List<ExchangeRate>();

        foreach (var dto in dtos)
        {
            var code = dto.CurrencyCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code) || !supported.Contains(code))
                continue;

            if (dto.BuyingRate <= 0 || dto.SellingRate <= 0)
                continue;

            rates.Add(new ExchangeRate
            {
                BaseCurrency = "LKR",
                TargetCurrency = code,
                BuyingRate = dto.BuyingRate,
                SellingRate = dto.SellingRate,
                Source = SourceName,
                CollectedAt = DateTime.UtcNow,
                RateDate = today
            });
        }

        logger.LogInformation("HNB: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal record HnbRateDto(
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("currencyCode")] string CurrencyCode,
        [property: JsonPropertyName("buyingRate")] decimal BuyingRate,
        [property: JsonPropertyName("sellingRate")] decimal SellingRate);
}
