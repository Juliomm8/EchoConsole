namespace EchoConsole.Web.Models.SessionEventAnalytics;

public sealed class SessionEventAnalyticsBucketViewModel
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }

    public decimal Percentage { get; set; }

    public decimal BarWidthPercentage { get; set; }
}