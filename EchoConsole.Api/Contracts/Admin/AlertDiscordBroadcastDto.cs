namespace EchoConsole.Api.Contracts.Admin;

public sealed class AlertDiscordBroadcastDto
{
    public bool Sent { get; set; }

    public int AlertCount { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
