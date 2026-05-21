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

    public async Task<UserDashboardResult> GetDashboardAsync(
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
                x.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return UserDashboardResult.UserNotFound();
        }

        var alias = user.Alias ?? user.Name ?? user.Email ?? "Player";

        var totalInstallations = await _dbContext.Installations
            .AsNoTracking()
            .CountAsync(x => x.OwnerUserId == userId, cancellationToken);

        var sessionsQuery = _dbContext.GameSessions
            .AsNoTracking()
            .Where(x => x.Installation.OwnerUserId == userId);

        var totalSessions = await sessionsQuery.CountAsync(cancellationToken);

        var favoriteBuild = await sessionsQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.BuildVersion))
            .GroupBy(x => x.BuildVersion)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefaultAsync(cancellationToken);

        var sessionTimes = await sessionsQuery
            .Select(x => new
            {
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.LastHeartbeatUtc
            })
            .ToListAsync(cancellationToken);

        var totalPlayTimeMinutes = 0;
        DateTimeOffset? lastActivityUtc = null;

        foreach (var session in sessionTimes)
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

        var dto = new UserDashboardDto
        {
            Alias = alias,
            TotalInstallations = totalInstallations,
            TotalSessions = totalSessions,
            TotalPlayTimeMinutes = totalPlayTimeMinutes,
            LastActivityUtc = lastActivityUtc,
            FavoriteBuild = string.IsNullOrWhiteSpace(favoriteBuild) ? "N/A" : favoriteBuild
        };

        _logger.LogInformation(
            "Built personal dashboard for user {UserId}. Installations={Installations}, Sessions={Sessions}.",
            userId,
            totalInstallations,
            totalSessions);

        return UserDashboardResult.Success(dto);
    }
}