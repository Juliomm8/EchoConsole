using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Builds;

public sealed class UpdateBuildInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    [StringLength(64)]
    public string VersionNumber { get; set; } = string.Empty;

    [StringLength(256)]
    public string? ReleaseNotes { get; set; }

    [Required]
    public DateTime ReleaseDateUtc { get; set; }

    [Required]
    [StringLength(64)]
    public string EngineVersion { get; set; } = string.Empty;
}
