using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Scrapers.Configuration;
using LkFxDashboard.Scrapers.Scrapers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LkFxDashboard.Scrapers.Extensions;

public static class ScraperServiceCollectionExtensions
{
    public static IServiceCollection AddScrapers(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScraperOptions>(
            configuration.GetSection(ScraperOptions.SectionName));

        // Register typed HttpClients for each scraper.
        // Use factory delegates for IExchangeRateScraper so that
        // GetRequiredService<T>() goes through the typed HttpClient factory.
        services.AddHttpClient<ScStandardCharteredScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddTransient<IExchangeRateScraper>(sp =>
            sp.GetRequiredService<ScStandardCharteredScraper>());

        services.AddHttpClient<CbslScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddTransient<IExchangeRateScraper>(sp =>
            sp.GetRequiredService<CbslScraper>());

        services.AddHttpClient<ComBankScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddTransient<IExchangeRateScraper>(sp =>
            sp.GetRequiredService<ComBankScraper>());

        services.AddHttpClient<HsbcPdfScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddTransient<IExchangeRateScraper>(sp =>
            sp.GetRequiredService<HsbcPdfScraper>());

        services.AddHttpClient<DfccScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddTransient<IExchangeRateScraper>(sp =>
            sp.GetRequiredService<DfccScraper>());

        services.AddHttpClient<HnbScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddTransient<IExchangeRateScraper>(sp =>
            sp.GetRequiredService<HnbScraper>());

        return services;
    }
}
