using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Domain.Entities;

public sealed class GameBuild
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string VersionNumber { get; set; } = null!;

    [MaxLength(256)]
    public string? ReleaseNotes { get; set; }

    public DateTimeOffset ReleaseDateUtc { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(64)]
    public string EngineVersion { get; set; } = null!;
}