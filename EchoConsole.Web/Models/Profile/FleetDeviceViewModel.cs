namespace EchoConsole.Web.Models.Profile;

public sealed class FleetDeviceViewModel
{
    public Guid InstallationId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string TelemetryStatus { get; set; } = "Inactive";

    public string CurrentScene { get; set; } = "N/A";

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }
}
