using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.PatchNotes;

public sealed class PatchNoteCreateViewModel
{
    [Required]
    [StringLength(32)]
    public string Version { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(green|amber|rose)$")]
    public string Tone { get; set; } = "green";

    [Required]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;
}
