namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileLiveSnapshotViewModel
{
    public string ConnectionStatus { get; set; } = "Offline";

    public bool HasActiveSession { get; set; }

    public Guid? ActiveSessionId { get; set; }

    public string CurrentScene { get; set; } = "N/A";

    public string CurrentGameState { get; set; } = "N/A";

    public string CurrentPhase { get; set; } = "N/A";

    public DateTimeOffset? SessionStartedAtUtc { get; set; }

    public DateTimeOffset? LastHeartbeatUtc { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; }
}
