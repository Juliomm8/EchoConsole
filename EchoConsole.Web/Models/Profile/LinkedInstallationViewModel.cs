namespace EchoConsole.Web.Models.Profile;

public sealed class LinkedInstallationViewModel
{
    public Guid InstallationId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string FirstSeenLabel { get; set; } = "N/A";

    public string LastUpdateLabel { get; set; } = "N/A";
}