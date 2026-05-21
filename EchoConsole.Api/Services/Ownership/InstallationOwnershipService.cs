using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.Ownership;

public sealed class InstallationOwnershipService : IInstallationOwnershipService
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<InstallationOwnershipService> _logger;

    public InstallationOwnershipService(
        EchoConsoleDbContext dbContext,
        ILogger<InstallationOwnershipService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ClaimInstallationResult> ClaimInstallationAsync(
        Guid installationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var installation = await _dbContext.Installations
            .FirstOrDefaultAsync(x => x.InstallationId == installationId, cancellationToken);

        if (installation is null)
        {
            return ClaimInstallationResult.InstallationNotFound(installationId);
        }

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
        {
            return ClaimInstallationResult.UserNotFound(installationId);
        }

        if (installation.OwnerUserId == userId)
        {
            return ClaimInstallationResult.AlreadyOwnedByCurrentUser(installationId, userId);
        }

        if (installation.OwnerUserId.HasValue && installation.OwnerUserId.Value != userId)
        {
            return ClaimInstallationResult.AlreadyOwnedByAnotherUser(installationId, installation.OwnerUserId);
        }

        installation.OwnerUserId = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Installation {InstallationId} was linked to user {UserId}.",
            installationId,
            userId);

        return ClaimInstallationResult.Success(installationId, userId);
    }
}