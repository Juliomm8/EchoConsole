namespace EchoConsole.Web.Models.Installations;

public sealed class InstallationRowViewModel
{
    public string InstallationId { get; set; } = string.Empty;

    public string GameCode { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string OSVersion { get; set; } = string.Empty;

    public string Processor { get; set; } = string.Empty;

    public string Gpu { get; set; } = string.Empty;

    public string RamLabel { get; set; } = string.Empty;

    public string TelemetryStatus { get; set; } = string.Empty;

    public string AdminAlias { get; set; } = string.Empty;

    public string AdminStatus { get; set; } = string.Empty;

    public string OwnerLabel { get; set; } = string.Empty;

    public string FirstSeenUtcLabel { get; set; } = string.Empty;

    public string LastUpdateUtcLabel { get; set; } = string.Empty;
}
