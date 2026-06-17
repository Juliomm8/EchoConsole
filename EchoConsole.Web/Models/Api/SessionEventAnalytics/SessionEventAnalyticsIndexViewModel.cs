namespace EchoConsole.Web.Models.SessionEventAnalytics;

public sealed class SessionEventAnalyticsIndexViewModel
{
    public string BuildVersion { get; set; } = string.Empty;

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int TotalEvents { get; set; }

    public int EventTypeCount { get; set; }

    public int SceneCount { get; set; }

    public int BuildCount { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public IReadOnlyList<string> AvailableBuildVersions { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<SessionEventAnalyticsBucketViewModel> EventTypes { get; set; } =
        Array.Empty<SessionEventAnalyticsBucketViewModel>();

    public IReadOnlyList<SessionEventAnalyticsBucketViewModel> Scenes { get; set; } =
        Array.Empty<SessionEventAnalyticsBucketViewModel>();

    public IReadOnlyList<SessionEventAnalyticsBucketViewModel> BuildVersions { get; set; } =
        Array.Empty<SessionEventAnalyticsBucketViewModel>();
}