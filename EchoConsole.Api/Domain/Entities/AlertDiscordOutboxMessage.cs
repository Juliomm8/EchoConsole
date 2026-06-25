using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Domain.Entities;

public sealed class AlertDiscordOutboxMessage
{
    public long Id { get; set; }

    public int SystemAlertId { get; set; }

    public SystemAlert SystemAlert { get; set; } = null!;

    public DateTimeOffset EnqueuedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset NextAttemptUtc { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }
}
