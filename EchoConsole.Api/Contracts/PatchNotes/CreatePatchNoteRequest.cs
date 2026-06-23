using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.PatchNotes;

public sealed class CreatePatchNoteRequest
{
    [Required]
    [MaxLength(32)]
    public string Version { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(green|amber|rose)$")]
    [MaxLength(16)]
    public string Tone { get; set; } = "green";

    [Required]
    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
}
