namespace EchoConsole.Web.Models.Home;

public sealed class HomeIndexViewModel
{
    public int TotalSessions { get; set; }

    public int ActivePlayersNow { get; set; }

    public int MonitoredBuilds { get; set; }

    public int OpenAlerts { get; set; }

    public string FeaturedBuildVersion { get; set; } = "N/A";
}