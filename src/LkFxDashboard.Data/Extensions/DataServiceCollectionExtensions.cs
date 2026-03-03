using LkFxDashboard.Core.Interfaces;
using LkFxDashboard.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LkFxDashboard.Data.Extensions;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
        return services;
    }
}
