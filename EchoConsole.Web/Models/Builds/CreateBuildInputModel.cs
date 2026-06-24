using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Builds;

public sealed class CreateBuildInputModel
{
    [Required]
    [StringLength(64)]
    public string VersionNumber { get; set; } = string.Empty;

    [StringLength(256)]
    public string? ReleaseNotes { get; set; }

    [Required]
    public DateTime ReleaseDateUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; }

    [Required]
    [StringLength(64)]
    public string EngineVersion { get; set; } = "Unity 2022.3.62f2";
}
