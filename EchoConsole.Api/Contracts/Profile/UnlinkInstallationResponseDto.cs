namespace EchoConsole.Api.Contracts.Profile;

public sealed class UnlinkInstallationResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public Guid InstallationId { get; set; }

    public int? PreviousOwnerUserId { get; set; }
}