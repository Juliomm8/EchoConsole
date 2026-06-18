namespace EchoConsole.Web.Models.Api.SessionEvents;

public sealed class AdminSessionTimelineDetailApiModel
{
    public Guid SessionId { get; set; }

    public Guid InstallationId { get; set; }

    public int? OwnerUserId { get; set; }

    public string OwnerAlias { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string CurrentScene { get; set; } = string.Empty;

    public string CurrentGameState { get; set; } = string.Empty;

    public string CurrentPhase { get; set; } = string.Empty;

    public int Status { get; set; }

    public string StatusLabel { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset LastHeartbeatUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public long DurationSeconds { get; set; }

    public int EventCount { get; set; }

    public IReadOnlyList<AdminSessionTimelineEventApiModel> Events { get; set; } =
        Array.Empty<AdminSessionTimelineEventApiModel>();
}