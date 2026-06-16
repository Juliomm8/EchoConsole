using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

        var linkedInstallationRawItems = await _dbContext.Installations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.LastUpdateUtc)
            .Select(x => new
            {
                x.InstallationId,
                x.DeviceName,
                x.DeviceModel,
                x.Platform,
                x.BuildVersion,
                x.Status,
                x.FirstSeenUtc,
                x.LastUpdateUtc
            })
            .ToListAsync(cancellationToken);

        var linkedInstallations = linkedInstallationRawItems
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
            .ToList();

        var sessionsQuery = _dbContext.GameSessions
            .AsNoTracking()
            .Where(x => x.Installation.OwnerUserId == userId);

        var totalSessions = await sessionsQuery
            .CountAsync(cancellationToken);

        var lastActivityUtc = await sessionsQuery
            .Select(x => (DateTimeOffset?)(x.EndedAtUtc ?? x.LastHeartbeatUtc))
            .MaxAsync(cancellationToken);

        var favoriteBuild = await sessionsQuery
            .Where(x => x.BuildVersion != null && x.BuildVersion != string.Empty)
            .GroupBy(x => x.BuildVersion)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefaultAsync(cancellationToken);

        var totalPlayTimeMinutes = await sessionsQuery
            .Where(x => (x.EndedAtUtc ?? x.LastHeartbeatUtc) >= x.StartedAtUtc)
            .Select(x => (int?)(
                x.EndedAtUtc.HasValue
                    ? EF.Functions.DateDiffMinute(x.StartedAtUtc, x.EndedAtUtc.Value)
                    : EF.Functions.DateDiffMinute(x.StartedAtUtc, x.LastHeartbeatUtc)
            ))
            .SumAsync(cancellationToken) ?? 0;

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
            TotalSessions = totalSessions,
            TotalPlayTimeMinutes = totalPlayTimeMinutes,
            LastActivityUtc = lastActivityUtc,
            FavoriteBuild = string.IsNullOrWhiteSpace(favoriteBuild) ? "N/A" : favoriteBuild,
            Installations = linkedInstallations
        };

        _logger.LogInformation(
            "Built optimized profile for user {UserId}. LinkedInstallations={LinkedInstallations}, Sessions={Sessions}.",
            userId,
            linkedInstallations.Count,
            totalSessions);

        return UserDashboardResult.Success(profile);
    }

    public async Task<UserSessionHistoryPageDto?> GetSessionHistoryAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
        {
            return null;
        }

        var query = _dbContext.GameSessions
            .AsNoTracking()
            .Where(x => x.Installation.OwnerUserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (page > totalPages)
        {
            page = totalPages;
        }

        var skip = (page - 1) * pageSize;

        var rawItems = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.LastHeartbeatUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new
            {
                x.SessionId,
                InstallationId = x.Installation.InstallationId,
                DeviceName = x.Installation.DeviceName,
                x.BuildVersion,
                x.CurrentScene,
                x.CurrentPhase,
                x.Status,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.LastHeartbeatUtc
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(x =>
            {
                var status = (int)x.Status;
                var endUtc = x.EndedAtUtc ?? x.LastHeartbeatUtc;
                var duration = endUtc - x.StartedAtUtc;

                var durationMinutes = duration > TimeSpan.Zero
                    ? (int)Math.Floor(duration.TotalMinutes)
                    : 0;

                return new UserSessionHistoryItemDto
                {
                    SessionId = x.SessionId,
                    InstallationId = x.InstallationId,
                    DeviceName = string.IsNullOrWhiteSpace(x.DeviceName) ? "-" : x.DeviceName,
                    BuildVersion = string.IsNullOrWhiteSpace(x.BuildVersion) ? "-" : x.BuildVersion,
                    CurrentScene = string.IsNullOrWhiteSpace(x.CurrentScene) ? "-" : x.CurrentScene,
                    CurrentPhase = string.IsNullOrWhiteSpace(x.CurrentPhase) ? "-" : x.CurrentPhase,
                    Status = status,
                    StatusLabel = MapSessionStatusLabel(status),
                    StartedAtUtc = x.StartedAtUtc,
                    EndedAtUtc = x.EndedAtUtc,
                    LastHeartbeatUtc = x.LastHeartbeatUtc,
                    DurationMinutes = durationMinutes,
                    IsLive = status == 1 && x.EndedAtUtc is null
                };
            })
            .ToList();

        return new UserSessionHistoryPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages,
            Items = items
        };
    }

    public async Task<UserSessionDetailDto?> GetSessionDetailAsync(
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
        {
            return null;
        }

        var rawSession = await _dbContext.GameSessions
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId && x.Installation.OwnerUserId == userId)
            .Select(x => new
            {
                x.SessionId,
                InstallationId = x.Installation.InstallationId,
                DeviceName = x.Installation.DeviceName,
                DeviceModel = x.Installation.DeviceModel,
                Platform = x.Installation.Platform,
                x.BuildVersion,
                x.CurrentScene,
                x.CurrentGameState,
                x.CurrentPhase,
                x.Status,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.LastHeartbeatUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (rawSession is null)
        {
            return null;
        }

        var status = (int)rawSession.Status;
        var endUtc = rawSession.EndedAtUtc ?? rawSession.LastHeartbeatUtc;
        var duration = endUtc - rawSession.StartedAtUtc;

        var durationMinutes = duration > TimeSpan.Zero
            ? (int)Math.Floor(duration.TotalMinutes)
            : 0;

        return new UserSessionDetailDto
        {
            SessionId = rawSession.SessionId,
            InstallationId = rawSession.InstallationId,
            DeviceName = string.IsNullOrWhiteSpace(rawSession.DeviceName) ? "-" : rawSession.DeviceName,
            DeviceModel = string.IsNullOrWhiteSpace(rawSession.DeviceModel) ? "-" : rawSession.DeviceModel,
            Platform = string.IsNullOrWhiteSpace(rawSession.Platform) ? "-" : rawSession.Platform,
            BuildVersion = string.IsNullOrWhiteSpace(rawSession.BuildVersion) ? "-" : rawSession.BuildVersion,
            CurrentScene = string.IsNullOrWhiteSpace(rawSession.CurrentScene) ? "-" : rawSession.CurrentScene,
            CurrentGameState = string.IsNullOrWhiteSpace(rawSession.CurrentGameState) ? "-" : rawSession.CurrentGameState,
            CurrentPhase = string.IsNullOrWhiteSpace(rawSession.CurrentPhase) ? "-" : rawSession.CurrentPhase,
            Status = status,
            StatusLabel = MapSessionStatusLabel(status),
            StartedAtUtc = rawSession.StartedAtUtc,
            EndedAtUtc = rawSession.EndedAtUtc,
            LastHeartbeatUtc = rawSession.LastHeartbeatUtc,
            DurationMinutes = durationMinutes,
            IsLive = status == 1 && rawSession.EndedAtUtc is null
        };
    }

    private static string MapSessionStatusLabel(int status)
    {
        return status switch
        {
            1 => "Live",
            2 => "Completed",
            3 => "Expired",
            _ => $"Unknown ({status})"
        };
    }
}