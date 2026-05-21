namespace EchoConsole.Web.Models.Api.Profile;

public sealed class LinkedInstallationApiModel
{
    public Guid InstallationId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastUpdateUtc { get; set; }
}