namespace EchoConsole.Api.Configuration;

public sealed class DiscordOptions
{
    public const string SectionName = "DiscordAlerts";

    public bool Enabled { get; set; }

    public string WebhookUrl { get; set; } = string.Empty;

    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 10;
}
