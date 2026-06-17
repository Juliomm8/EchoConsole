namespace EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;

public sealed class AdminSessionEventAnalyticsDto
{
    public int TotalEvents { get; set; }

    public string AppliedBuildVersion { get; set; } = string.Empty;

    public DateTimeOffset? AppliedFromUtc { get; set; }

    public DateTimeOffset? AppliedToUtcExclusive { get; set; }

    public IReadOnlyList<string> AvailableBuildVersions { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<AdminSessionEventAnalyticsBucketDto> EventTypes { get; set; } =
        Array.Empty<AdminSessionEventAnalyticsBucketDto>();

    public IReadOnlyList<AdminSessionEventAnalyticsBucketDto> Scenes { get; set; } =
        Array.Empty<AdminSessionEventAnalyticsBucketDto>();

    public IReadOnlyList<AdminSessionEventAnalyticsBucketDto> BuildVersions { get; set; } =
        Array.Empty<AdminSessionEventAnalyticsBucketDto>();
}