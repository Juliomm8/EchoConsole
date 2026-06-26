namespace EchoConsole.Api.Contracts.Admin;

public sealed class GameBuildSummaryDto
{
    public int TotalBuilds { get; set; }

    public string ActiveVersion { get; set; } = string.Empty;

    public string BaseEngineVersion { get; set; } = string.Empty;
}
