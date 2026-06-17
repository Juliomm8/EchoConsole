namespace EchoConsole.Web.Models.Api.SessionEventAnalytics;

public sealed class AdminSessionEventAnalyticsApiModel
{
    public int TotalEvents { get; set; }

    public string AppliedBuildVersion { get; set; } = string.Empty;

    public DateTimeOffset? AppliedFromUtc { get; set; }

    public DateTimeOffset? AppliedToUtcExclusive { get; set; }

    public IReadOnlyList<string> AvailableBuildVersions { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<AdminSessionEventAnalyticsBucketApiModel> EventTypes { get; set; } =
        Array.Empty<AdminSessionEventAnalyticsBucketApiModel>();

    public IReadOnlyList<AdminSessionEventAnalyticsBucketApiModel> Scenes { get; set; } =
        Array.Empty<AdminSessionEventAnalyticsBucketApiModel>();

    public IReadOnlyList<AdminSessionEventAnalyticsBucketApiModel> BuildVersions { get; set; } =
        Array.Empty<AdminSessionEventAnalyticsBucketApiModel>();
}