namespace EchoConsole.Api.Contracts.Admin;

public sealed class BuildOperationErrorResponse
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int LinkedInstallations { get; set; }

    public int TotalSessions { get; set; }
}
