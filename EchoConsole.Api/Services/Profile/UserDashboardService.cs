using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.Profile;

public sealed class UserDashboardService : IUserDashboardService
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<UserDashboardService> _logger;

    public UserDashboardService(
        EchoConsoleDbContext dbContext,
        ILogger<UserDashboardService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserDashboardResult> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.Alias,
                x.Name,
                x.Email,
                x.AvatarKey,
                x.Theme,
                x.Role,
                x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return UserDashboardResult.UserNotFound();
        }

        var linkedInstallations = await _dbContext.Installations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.LastUpdateUtc)
            .Select(x => new LinkedInstallationDto
            {
                InstallationId = x.InstallationId,
                DeviceName = string.IsNullOrWhiteSpace(x.DeviceName) ? "-" : x.DeviceName,
                DeviceModel = string.IsNullOrWhiteSpace(x.DeviceModel) ? "-" : x.DeviceModel,
                Platform = string.IsNullOrWhiteSpace(x.Platform) ? "-" : x.Platform,
                BuildVersion = string.IsNullOrWhiteSpace(x.BuildVersion) ? "-" : x.BuildVersion,
                Status = string.IsNullOrWhiteSpace(x.Status) ? "-" : x.Status,
                FirstSeenUtc = x.FirstSeenUtc,
                LastUpdateUtc = x.LastUpdateUtc
            })
            .ToListAsync(cancellationToken);

        var sessions = await _dbContext.GameSessions
            .AsNoTracking()
            .Where(x => x.Installation.OwnerUserId == userId)
            .Select(x => new
            {
                x.BuildVersion,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.LastHeartbeatUtc
            })
            .ToListAsync(cancellationToken);

        var totalPlayTimeMinutes = 0;
        DateTimeOffset? lastActivityUtc = null;

        foreach (var session in sessions)
        {
            var endUtc = session.EndedAtUtc ?? session.LastHeartbeatUtc;
            var duration = endUtc - session.StartedAtUtc;

            if (duration > TimeSpan.Zero)
            {
                totalPlayTimeMinutes += (int)Math.Floor(duration.TotalMinutes);
            }

            if (lastActivityUtc is null || endUtc > lastActivityUtc.Value)
            {
                lastActivityUtc = endUtc;
            }
        }

        var favoriteBuild = sessions
            .Where(x => !string.IsNullOrWhiteSpace(x.BuildVersion))
            .GroupBy(x => x.BuildVersion)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        var profile = new UserProfileDto
        {
            UserId = user.Id,
            Alias = user.Alias ?? user.Name ?? user.Email ?? "Player",
            Name = user.Name ?? string.Empty,
            Email = user.Email ?? string.Empty,
            AvatarKey = string.IsNullOrWhiteSpace(user.AvatarKey) ? "avatar-01" : user.AvatarKey,
            Theme = string.IsNullOrWhiteSpace(user.Theme) ? "cyan" : user.Theme,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            TotalInstallations = linkedInstallations.Count,
            TotalSessions = sessions.Count,
            TotalPlayTimeMinutes = totalPlayTimeMinutes,
            LastActivityUtc = lastActivityUtc,
            FavoriteBuild = string.IsNullOrWhiteSpace(favoriteBuild) ? "N/A" : favoriteBuild,
            Installations = linkedInstallations
        };

        _logger.LogInformation(
            "Built profile for user {UserId}. LinkedInstallations={LinkedInstallations}, Sessions={Sessions}.",
            userId,
            linkedInstallations.Count,
            sessions.Count);

        return UserDashboardResult.Success(profile);
    }
}