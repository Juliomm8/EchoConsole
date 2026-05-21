namespace EchoConsole.Api.Services.Ownership;

public interface IInstallationOwnershipService
{
    Task<ClaimInstallationResult> ClaimInstallationAsync(
        Guid installationId,
        int userId,
        CancellationToken cancellationToken = default);
}