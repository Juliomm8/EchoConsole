namespace EchoConsole.Web.Models.SessionEventAnalytics;

public sealed class SessionEventAnalyticsTimePointViewModel
{
    public string Label { get; set; } = string.Empty;

    public string IsoUtc { get; set; } = string.Empty;

    public int Count { get; set; }
}