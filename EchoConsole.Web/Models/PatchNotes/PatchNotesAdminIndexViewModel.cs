namespace EchoConsole.Web.Models.PatchNotes;

public sealed class PatchNotesAdminIndexViewModel
{
    public IReadOnlyList<PatchNotesAdminListItemViewModel> Items { get; set; } =
        Array.Empty<PatchNotesAdminListItemViewModel>();

    public PatchNoteUpdateViewModel Edit { get; set; } = new();
}

public sealed class PatchNotesAdminListItemViewModel
{
    public int Id { get; set; }

    public string Version { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tone { get; set; } = "green";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsPublished { get; set; }
}
