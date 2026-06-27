namespace EchoConsole.Web.Models.Profile;

public sealed class ActiveUserSessionViewModel
{
    public long Id { get; set; }

    public string Browser { get; set; } = "Unknown browser";

    public string OperatingSystem { get; set; } = "Unknown system";

    public string DeviceLabel { get; set; } = "Unknown browser on Unknown system";

    public string MaskedIpAddress { get; set; } = "Unknown";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public bool IsCurrent { get; set; }
}
