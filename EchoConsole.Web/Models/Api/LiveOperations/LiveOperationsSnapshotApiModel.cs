namespace EchoConsole.Web.Models.Api.LiveOperations;

public sealed class LiveOperationsSnapshotApiModel
{
    public DateTimeOffset ServerTimeUtc { get; set; }

    public int TotalInstallations { get; set; }

    public int ActiveInstallations { get; set; }

    public int DegradedInstallations { get; set; }

    public int InactiveInstallations { get; set; }

    public int ActiveSessions { get; set; }

    public int EventsLast5Minutes { get; set; }

    public int EventsLast15Minutes { get; set; }

    public int PreviousFiveMinuteEvents { get; set; }

    public int AlertsLast15Minutes { get; set; }

    public int UnresolvedAlerts { get; set; }

    public decimal AlertRatePerMinute { get; set; }

    public string EventSpikeState { get; set; } = "Quiet";

    public decimal EventSpikeMultiplier { get; set; }

    public IReadOnlyList<LiveOperationsInstallationApiModel> Installations { get; set; } =
        Array.Empty<LiveOperationsInstallationApiModel>();
}