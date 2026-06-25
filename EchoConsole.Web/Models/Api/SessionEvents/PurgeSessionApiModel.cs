namespace EchoConsole.Web.Models.Api.SessionEvents;

public sealed class PurgeSessionApiModel
{
    public Guid SessionId { get; set; }

    public int DeletedEventCount { get; set; }

    public int DeletedMatchingEventCount { get; set; }

    public DateTimeOffset PurgedAtUtc { get; set; }
}
