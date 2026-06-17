namespace EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;

public sealed class AdminSessionEventAnalyticsBucketDto
{
    public string Key { get; set; } = string.Empty;

    public int Count { get; set; }
}