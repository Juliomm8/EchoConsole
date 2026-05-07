using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Api.Contracts.Dashboard;

public sealed class LiveSessionDto
{
    public Guid SessionId { get; set; }

    public Guid InstallationId { get; set; }

    public string BuildVersion { get; set; } = null!;

    public string CurrentScene { get; set; } = null!;

    public string CurrentGameState { get; set; } = null!;

    public string? CurrentPhase { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset LastHeartbeatUtc { get; set; }

    public SessionStatus Status { get; set; }
}