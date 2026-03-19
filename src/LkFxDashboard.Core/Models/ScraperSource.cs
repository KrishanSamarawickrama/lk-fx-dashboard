namespace LkFxDashboard.Core.Models;

public static class ScraperSource
{
    public const string StandardChartered = "Standard Chartered";
    public const string Cbsl = "CBSL";
    public const string ComBank = "Commercial Bank";
    public const string Hsbc = "HSBC";
    public const string Dfcc = "DFCC";

    public static readonly IReadOnlyList<string> All =
    [
        StandardChartered,
        Cbsl,
        ComBank,
        Hsbc,
        Dfcc
    ];
}
