using System.ComponentModel.DataAnnotations;

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

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }

    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
}