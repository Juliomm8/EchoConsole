using System.ComponentModel.DataAnnotations;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Api.Domain.Entities;

public sealed class SystemAlert
{
    public int Id { get; set; }

    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;

    [MaxLength(64)]
    public string ErrorTypeCode { get; set; } = "UNCLASSIFIED";

    [MaxLength(64)]
    public string? BuildVersion { get; set; }

    [MaxLength(500)]
    public string Message { get; set; } = null!;

    [MaxLength(128)]
    public string Source { get; set; } = null!;

    [MaxLength(64)]
    public string? InstallationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public ICollection<AlertDiscordOutboxMessage> DiscordOutboxMessages { get; set; } =
        new List<AlertDiscordOutboxMessage>();
}
