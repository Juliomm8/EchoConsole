namespace EchoConsole.Api.Contracts.Admin;

public sealed class AlertAiTrendAnalysisDto
{
    public string Narrative { get; set; } = string.Empty;

    public string ActiveBuildVersion { get; set; } = string.Empty;

    public string DominantSource { get; set; } = string.Empty;

    public int RecentAlertCount { get; set; }

    public int OpenAlertCount { get; set; }

    public int MitigatedLast24Hours { get; set; }

    public int ActiveCriticalCount { get; set; }

    public int RecentCriticalCount { get; set; }

    public int PreviousCriticalCount { get; set; }

    public decimal CriticalTrendPercent { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }
}
