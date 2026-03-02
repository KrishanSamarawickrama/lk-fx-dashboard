namespace LkFxDashboard.Core.Models;

public class ExchangeRate
{
    public int Id { get; set; }
    public string BaseCurrency { get; set; } = "LKR";
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal BuyingRate { get; set; }
    public decimal SellingRate { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public DateOnly RateDate { get; set; }
}
