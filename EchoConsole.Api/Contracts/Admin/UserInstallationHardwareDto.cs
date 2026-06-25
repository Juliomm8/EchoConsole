namespace EchoConsole.Api.Contracts.Admin;

public sealed class UserInstallationHardwareDto
{
    public Guid InstallationId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string? Cpu { get; set; }

    public string? Gpu { get; set; }

    public int? RamMb { get; set; }

    public string OSVersion { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string AdminStatus { get; set; } = string.Empty;

    public DateTimeOffset LastUpdateUtc { get; set; }
}
