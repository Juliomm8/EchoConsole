namespace EchoConsole.Web.Models.Home;

public sealed class HomeIndexViewModel
{
    // Public marketing / community data
    public int TotalSessions { get; set; }

    public int ActivePlayersNow { get; set; }

    public int MonitoredBuilds { get; set; }

    public int OpenAlerts { get; set; }

    public string FeaturedBuildVersion { get; set; } = "N/A";

    // Authenticated user block
    public bool IsAuthenticated { get; set; }

    public string UserAlias { get; set; } = "Player";

    public int UserTotalSessions { get; set; }

    public string UserLastActivity { get; set; } = "N/A";

    public string UserFavoriteBuild { get; set; } = "N/A";
}