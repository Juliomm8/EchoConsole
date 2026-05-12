namespace EchoConsole.Api.Contracts.Admin;

public sealed class GameBuildDto
{
    public int Id { get; set; }

    public string VersionNumber { get; set; } = string.Empty;

    public string? ReleaseNotes { get; set; }

    public DateTimeOffset ReleaseDateUtc { get; set; }

    public bool IsActive { get; set; }

    public string EngineVersion { get; set; } = string.Empty;
}