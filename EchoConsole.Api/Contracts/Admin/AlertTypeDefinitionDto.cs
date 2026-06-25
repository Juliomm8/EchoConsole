namespace EchoConsole.Api.Contracts.Admin;

public sealed class AlertTypeDefinitionDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DefaultSeverity { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int AlertCount { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
