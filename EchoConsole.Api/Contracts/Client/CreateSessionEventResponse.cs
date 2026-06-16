namespace EchoConsole.Api.Contracts.Client;

public sealed class CreateSessionEventResponse
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Scene { get; set; }

    public string? GameState { get; set; }

    public string? Phase { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; }
}