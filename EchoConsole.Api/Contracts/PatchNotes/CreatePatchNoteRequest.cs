using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.PatchNotes;

public sealed class CreatePatchNoteRequest
{
    [Required(ErrorMessage = "Version is required.")]
    [MaxLength(32)]
    public string Version { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [MaxLength(32)]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tone is required.")]
    [RegularExpression(
        "^(green|amber|rose)$",
        ErrorMessage = "Tone must be green, amber or rose.")]
    [MaxLength(16)]
    public string Tone { get; set; } = "green";

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(
        160,
        MinimumLength = 5,
        ErrorMessage = "Title must contain between 5 and 160 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(
        4000,
        MinimumLength = 10,
        ErrorMessage = "Description must contain between 10 and 4000 characters.")]
    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
}
