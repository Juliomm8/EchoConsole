namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileSessionEventTimelineItemViewModel
{
    public long Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public string GameState { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string ClientTimeLabel { get; set; } = string.Empty;

    public string CreatedAtLabel { get; set; } = string.Empty;

    public bool HasPayload { get; set; }
}