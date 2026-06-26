using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Domain.Entities;

public sealed class UserSession
{
    public long Id { get; set; }

    public int UserId { get; set; }

    [MaxLength(64)]
    public string SessionKeyHash { get; set; } = null!;

    [MaxLength(512)]
    public string UserAgent { get; set; } = "Unknown";

    [MaxLength(64)]
    public string MaskedIpAddress { get; set; } = "Unknown";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    [MaxLength(128)]
    public string? RevokedReason { get; set; }

    public User User { get; set; } = null!;
}
