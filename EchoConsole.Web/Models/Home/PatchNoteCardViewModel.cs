namespace EchoConsole.Web.Models.Home;

public sealed class PatchNoteCardViewModel
{
    public int Id { get; set; }

    public string Version { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tone { get; set; } = "green";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
