namespace EchoConsole.Web.Models.Dashboard;

public sealed class LiveSessionRowViewModel
{
    public string SessionId { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public string CurrentScene { get; set; } = string.Empty;

    public string GameState { get; set; } = string.Empty;

    public string CurrentPhase { get; set; } = string.Empty;

    public string LastHeartbeatLabel { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = "Active";
}