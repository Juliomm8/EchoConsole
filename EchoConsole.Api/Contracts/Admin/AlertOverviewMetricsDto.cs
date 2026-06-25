namespace EchoConsole.Api.Contracts.Admin;

public sealed class AlertOverviewMetricsDto
{
    public int ActiveNocCount { get; set; }

    public int MitigatedLast24Hours { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }
}
