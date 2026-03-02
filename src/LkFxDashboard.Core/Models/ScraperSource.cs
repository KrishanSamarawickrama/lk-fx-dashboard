namespace LkFxDashboard.Core.Models;

public static class ScraperSource
{
    public const string StandardChartered = "Standard Chartered";
    public const string Cbsl = "CBSL";
    public const string Oanda = "OANDA";
    public const string Hsbc = "HSBC";

    public static readonly IReadOnlyList<string> All =
    [
        StandardChartered,
        Cbsl,
        Oanda,
        Hsbc
    ];
}
