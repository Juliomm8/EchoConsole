namespace EchoConsole.Web.Models.Api.Profile;

public sealed class UserSessionEventApiModel
{
    public long Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public string GameState { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset? ClientTimeUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}