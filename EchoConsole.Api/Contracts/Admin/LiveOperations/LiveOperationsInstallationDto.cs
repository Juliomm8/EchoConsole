namespace EchoConsole.Api.Contracts.Admin.LiveOperations;

public sealed class LiveOperationsInstallationDto
{
    public Guid InstallationId { get; set; }

    public int? OwnerUserId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string OperationalState { get; set; } = string.Empty;

    public string CurrentScene { get; set; } = string.Empty;

    public string CurrentGameState { get; set; } = string.Empty;

    public DateTimeOffset LastUpdateUtc { get; set; }

    public DateTimeOffset? LastHeartbeatUtc { get; set; }
}