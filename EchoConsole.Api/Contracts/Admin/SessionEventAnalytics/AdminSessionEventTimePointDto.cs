namespace EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;

public sealed class AdminSessionEventTimePointDto
{
    public DateTimeOffset BucketStartUtc { get; set; }

    public int Count { get; set; }
}