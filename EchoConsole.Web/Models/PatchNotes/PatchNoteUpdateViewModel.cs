using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.PatchNotes;

public sealed class PatchNoteUpdateViewModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Version is required.")]
    [StringLength(
        32,
        ErrorMessage = "Version cannot exceed 32 characters.")]
    public string Version { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(
        32,
        ErrorMessage = "Category cannot exceed 32 characters.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tone is required.")]
    [RegularExpression(
        "^(green|amber|rose)$",
        ErrorMessage = "Tone must be green, amber or rose.")]
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

    public bool IsPublished { get; set; }
}
