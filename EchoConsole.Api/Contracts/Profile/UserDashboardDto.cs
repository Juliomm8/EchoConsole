namespace EchoConsole.Api.Contracts.Profile;

public sealed class UserDashboardDto
{
    public string Alias { get; set; } = "Player";

    public int TotalInstallations { get; set; }

    public int TotalSessions { get; set; }

    public int TotalPlayTimeMinutes { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    public string FavoriteBuild { get; set; } = "N/A";
}