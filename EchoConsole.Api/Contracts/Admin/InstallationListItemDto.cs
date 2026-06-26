namespace EchoConsole.Api.Contracts.Admin;

public sealed class InstallationListItemDto
{
    public int DatabaseId { get; set; }

    public Guid InstallationId { get; set; }

    public string GameCode { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public string? Processor { get; set; }

    public string? Gpu { get; set; }

    public int? RamMb { get; set; }

    public string TelemetryStatus { get; set; } = string.Empty;

    public string? AdminAlias { get; set; }

    public string AdminStatus { get; set; } = string.Empty;

    public int? OwnerUserId { get; set; }

    public string? OwnerAlias { get; set; }

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }
}
