namespace EchoConsole.Api.Contracts.Admin;

public sealed class InstallationListItemDto
{
    public Guid InstallationId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public string? Processor { get; set; }

    public string? Gpu { get; set; }

    public int? RamMb { get; set; }

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }
}