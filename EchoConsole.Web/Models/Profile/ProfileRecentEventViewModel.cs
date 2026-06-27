namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileRecentEventViewModel
{
    public long Id { get; set; }

    public string EventType { get; set; } = "TelemetryEvent";

    public string Scene { get; set; } = "N/A";

    public DateTimeOffset OccurredAtUtc { get; set; }
}
