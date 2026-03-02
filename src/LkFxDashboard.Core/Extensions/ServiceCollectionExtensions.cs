using LkFxDashboard.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LkFxDashboard.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static readonly IReadOnlyList<CurrencyInfo> DefaultCurrencies =
    [
        new("USD", "US Dollar"),
        new("EUR", "Euro"),
        new("GBP", "British Pound"),
        new("CHF", "Swiss Franc"),
        new("JPY", "Japanese Yen"),
        new("AUD", "Australian Dollar"),
        new("CAD", "Canadian Dollar"),
        new("SGD", "Singapore Dollar"),
        new("INR", "Indian Rupee"),
    ];
}
