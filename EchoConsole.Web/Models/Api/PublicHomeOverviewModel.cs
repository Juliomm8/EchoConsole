namespace EchoConsole.Web.Models.Api;

public sealed class PublicHomeOverviewModel
{
    public int TotalSessions { get; set; }

    public int ActivePlayersNow { get; set; }

    public int MonitoredBuilds { get; set; }

    public int OpenAlerts { get; set; }

    public string FeaturedBuildVersion { get; set; } = "N/A";
}