namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileSessionDetailViewModel
{
    public Guid SessionId { get; set; }

    public Guid InstallationId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string CurrentScene { get; set; } = string.Empty;

    public string CurrentGameState { get; set; } = string.Empty;

    public string CurrentPhase { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public bool IsLive { get; set; }

    public string StartedAtLabel { get; set; } = string.Empty;

    public string EndedAtLabel { get; set; } = string.Empty;

    public string LastHeartbeatLabel { get; set; } = string.Empty;

    public string DurationLabel { get; set; } = string.Empty;
}