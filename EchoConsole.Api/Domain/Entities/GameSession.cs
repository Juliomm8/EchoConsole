using System.ComponentModel.DataAnnotations;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Api.Domain.Entities;

public sealed class GameSession
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public int InstallationDbId { get; set; }

    public Installation Installation { get; set; } = null!;

    [MaxLength(128)]
    public string SessionTokenHash { get; set; } = null!;

    [MaxLength(32)]
    public string BuildVersion { get; set; } = null!;

    [MaxLength(128)]
    public string CurrentScene { get; set; } = "Unknown";

    [MaxLength(64)]
    public string CurrentGameState { get; set; } = "Unknown";

    [MaxLength(64)]
    public string? CurrentPhase { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset LastHeartbeatUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Active;

    public ICollection<GameSessionEvent> Events { get; set; } = new List<GameSessionEvent>();
}