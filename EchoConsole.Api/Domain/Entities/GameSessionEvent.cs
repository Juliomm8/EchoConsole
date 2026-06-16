using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Domain.Entities;

public sealed class GameSessionEvent
{
    public long Id { get; set; }

    public long GameSessionId { get; set; }

    public GameSession GameSession { get; set; } = null!;

    [MaxLength(64)]
    public string EventType { get; set; } = null!;

    [MaxLength(128)]
    public string? Scene { get; set; }

    [MaxLength(64)]
    public string? GameState { get; set; }

    [MaxLength(64)]
    public string? Phase { get; set; }

    [MaxLength(4000)]
    public string? PayloadJson { get; set; }

    public DateTimeOffset? ClientTimeUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}