namespace EchoConsole.Web.Models.SessionEvents;

public sealed class AdminSessionTimelineEventViewModel
{
    public int SequenceNumber { get; set; }

    public long Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public string GameState { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public bool HasPayload { get; set; }

    public string PrimaryTimeLabel { get; set; } = string.Empty;

    public string PrimaryTimeSource { get; set; } = string.Empty;

    public string ServerTimeLabel { get; set; } = string.Empty;

    public string ClientTimeLabel { get; set; } = string.Empty;
}