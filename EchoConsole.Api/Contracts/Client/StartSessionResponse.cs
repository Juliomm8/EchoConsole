namespace EchoConsole.Api.Contracts.Client;

public sealed class StartSessionResponse
{
    public Guid SessionId { get; set; }

    public string SessionToken { get; set; } = null!;

    public int HeartbeatIntervalSeconds { get; set; }

    public int HeartbeatTimeoutSeconds { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; }
}