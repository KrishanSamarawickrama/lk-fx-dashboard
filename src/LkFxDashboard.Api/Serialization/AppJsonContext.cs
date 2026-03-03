using System.Text.Json.Serialization;
using LkFxDashboard.Core.Models;

namespace LkFxDashboard.Api.Serialization;

[JsonSerializable(typeof(List<ExchangeRate>))]
[JsonSerializable(typeof(ExchangeRate))]
[JsonSerializable(typeof(List<CurrencyInfo>))]
[JsonSerializable(typeof(CurrencyInfo))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonContext : JsonSerializerContext;
