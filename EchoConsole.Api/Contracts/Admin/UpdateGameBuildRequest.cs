using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Admin;

public sealed class UpdateGameBuildRequest
{
    [Required]
    [MaxLength(64)]
    public string VersionNumber { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ReleaseNotes { get; set; }

    [Required]
    public DateTimeOffset ReleaseDateUtc { get; set; }

    [Required]
    [MaxLength(64)]
    public string EngineVersion { get; set; } = string.Empty;
}
