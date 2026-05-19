namespace EchoConsole.Api.Contracts.Public;

public sealed class PublicHomeOverviewDto
{
    public int TotalSessions { get; set; }

    public int ActivePlayersNow { get; set; }

    public int MonitoredBuilds { get; set; }

    public int OpenAlerts { get; set; }

    public string FeaturedBuildVersion { get; set; } = "N/A";
}