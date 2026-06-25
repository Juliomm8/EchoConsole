namespace EchoConsole.Api.Contracts.Admin;

public sealed class UserListItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public int InstallationCount { get; set; }

    public DateTimeOffset? LastTelemetryUtc { get; set; }

    public IReadOnlyList<UserInstallationHardwareDto> Installations { get; set; } =
        Array.Empty<UserInstallationHardwareDto>();
}
