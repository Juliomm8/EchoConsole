namespace EchoConsole.Web.Models.Api.SessionEventAnalytics;

public sealed class AdminSessionEventTimePointApiModel
{
    public DateTimeOffset BucketStartUtc { get; set; }

    public int Count { get; set; }
}