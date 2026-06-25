using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EchoConsole.Api.Domain.Entities;

public sealed class Installation
{
    public int Id { get; set; }

    public Guid InstallationId { get; set; }

    [MaxLength(64)]
    public string GameCode { get; set; } = null!;

    [MaxLength(32)]
    public string BuildVersion { get; set; } = null!;

    [MaxLength(32)]
    public string Platform { get; set; } = null!;

    [MaxLength(128)]
    public string DeviceName { get; set; } = null!;

    [MaxLength(128)]
    public string DeviceModel { get; set; } = null!;

    [MaxLength(128)]
    public string OSVersion { get; set; } = null!;

    [MaxLength(128)]
    public string? Processor { get; set; }

    [MaxLength(128)]
    public string? Gpu { get; set; }

    public int? RamMb { get; set; }

    [MaxLength(24)]
    public string Status { get; set; } = "Active";

    [MaxLength(128)]
    [Column(TypeName = "nvarchar(128)")]
    public string? AdminAlias { get; set; }

    [Required]
    [MaxLength(24)]
    [Column(TypeName = "nvarchar(24)")]
    public string AdminStatus { get; set; } = "Active";

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }

    public ICollection<GameSession> Sessions { get; set; } =
        new List<GameSession>();

    public int? OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }
}
