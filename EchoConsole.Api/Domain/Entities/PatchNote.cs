using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Domain.Entities;

public sealed class PatchNote
{
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string Version { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Tone { get; set; } = "green";

    [Required]
    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public bool IsPublished { get; set; }
}
