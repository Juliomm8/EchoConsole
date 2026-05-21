namespace EchoConsole.Api.Contracts.Profile;

public sealed class ClaimInstallationResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public Guid InstallationId { get; set; }

    public int? OwnerUserId { get; set; }
}