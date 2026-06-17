namespace EchoConsole.Api.Contracts.Admin.SessionEvents;

public sealed class AdminSessionEventItemDto
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid InstallationId { get; set; }

    public int? OwnerUserId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public string GameState { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset? ClientTimeUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}