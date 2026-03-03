namespace LkFxDashboard.Api.Security;

public class SecurityOptions
{
    public const string SectionName = "Security";

    public string ApiKey { get; set; } = string.Empty;
    public string AdminPin { get; set; } = string.Empty;
}
