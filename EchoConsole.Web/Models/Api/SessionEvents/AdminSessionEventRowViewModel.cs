namespace EchoConsole.Web.Models.SessionEvents;

public sealed class AdminSessionEventRowViewModel
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid InstallationId { get; set; }

    public string OwnerUserLabel { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public string GameState { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public bool HasPayload { get; set; }

    public string ClientTimeLabel { get; set; } = string.Empty;

    public string CreatedAtLabel { get; set; } = string.Empty;
}