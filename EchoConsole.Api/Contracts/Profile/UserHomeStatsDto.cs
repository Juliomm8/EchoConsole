namespace EchoConsole.Api.Contracts.Profile;

public sealed class UserHomeStatsDto
{
    public string Alias { get; set; } = "Player";

    public int TotalSessions { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    public string FavoriteBuild { get; set; } = "N/A";
}