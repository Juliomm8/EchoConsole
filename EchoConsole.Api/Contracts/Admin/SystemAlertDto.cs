namespace EchoConsole.Api.Contracts.Admin;

public sealed class SystemAlertDto
{
    public int Id { get; set; }

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? InstallationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }
}