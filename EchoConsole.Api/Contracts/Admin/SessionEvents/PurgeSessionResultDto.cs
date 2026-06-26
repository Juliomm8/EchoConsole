namespace EchoConsole.Api.Contracts.Admin.SessionEvents;

public sealed class PurgeSessionResultDto
{
    public Guid SessionId { get; set; }

    public int DeletedEventCount { get; set; }

    public int DeletedMatchingEventCount { get; set; }

    public DateTimeOffset PurgedAtUtc { get; set; }
}
