namespace EchoConsole.Web.Models.Api.Profile;

public sealed class UserSessionDetailApiModel
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

    public int Status { get; set; }

    public string StatusLabel { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public DateTimeOffset LastHeartbeatUtc { get; set; }

    public int DurationMinutes { get; set; }

    public bool IsLive { get; set; }

    public IReadOnlyList<UserSessionEventApiModel> Events { get; set; } = Array.Empty<UserSessionEventApiModel>();
}