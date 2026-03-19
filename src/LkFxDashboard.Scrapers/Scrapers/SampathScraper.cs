using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Core.Models;
using LkFxDashboard.Scrapers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LkFxDashboard.Scrapers.Scrapers;

/// <summary>
/// Fetches exchange rates from Sampath Bank's JSON API.
/// The endpoint returns a wrapper object with a "data" array of rate objects.
/// Rate values are returned as strings and need decimal parsing.
/// </summary>
public class SampathScraper(
    HttpClient httpClient,
    IOptions<ScraperOptions> options,
    ILogger<SampathScraper> logger) : IExchangeRateScraper
{
    public string SourceName => ScraperSource.Sampath;

    public IReadOnlyList<string> SupportedCurrencies =>
        ["USD", "GBP", "EUR", "JPY", "AUD", "NZD", "CHF", "SEK", "DKK",
         "CAD", "SGD", "HKD", "NOK", "CNY", "ZAR", "AED", "INR"];

    public async Task<IReadOnlyList<ExchangeRate>> FetchRatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            logger.LogInformation("Fetching rates from Sampath: {Url}", config.SampathUrl);

            await Task.Delay(config.PoliteDelayMs, cancellationToken);

            var response = await httpClient.GetAsync(config.SampathUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            return ParseJson(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Sampath rates");
            return [];
        }
    }

    internal IReadOnlyList<ExchangeRate> ParseJson(string json)
    {
        var wrapper = JsonSerializer.Deserialize<SampathApiResponse>(json, JsonOptions);
        if (wrapper?.Data is null || wrapper.Data.Count == 0)
            return [];

        var supported = new HashSet<string>(SupportedCurrencies, StringComparer.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rates = new List<ExchangeRate>();

        foreach (var dto in wrapper.Data)
        {
            var code = dto.CurrCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code) || !supported.Contains(code))
                continue;

            if (!TryParseDecimal(dto.TtBuy, out var buyRate) ||
                !TryParseDecimal(dto.TtSel, out var sellRate))
                continue;

            if (buyRate <= 0 || sellRate <= 0)
                continue;

            rates.Add(new ExchangeRate
            {
                BaseCurrency = "LKR",
                TargetCurrency = code,
                BuyingRate = buyRate,
                SellingRate = sellRate,
                Source = SourceName,
                CollectedAt = DateTime.UtcNow,
                RateDate = today
            });
        }

        logger.LogInformation("Sampath: parsed {Count} rates", rates.Count);
        return rates;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return decimal.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal record SampathApiResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("data")] List<SampathRateDto> Data);

    internal record SampathRateDto(
        [property: JsonPropertyName("CurrCode")] string CurrCode,
        [property: JsonPropertyName("CurrName")] string CurrName,
        [property: JsonPropertyName("TTBUY")] string TtBuy,
        [property: JsonPropertyName("TTSEL")] string TtSel,
        [property: JsonPropertyName("ODBUY")] string OdBuy);
}
