using EchoConsole.Api.Contracts.Admin.SessionEvents;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.SessionEvents;

public sealed class AdminSessionEventsService : IAdminSessionEventsService
{
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 50;

    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<AdminSessionEventsService> _logger;

    public AdminSessionEventsService(
        EchoConsoleDbContext dbContext,
        ILogger<AdminSessionEventsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminSessionEventsPageDto> GetRecentEventsAsync(
        string? eventType,
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1
            ? DefaultPageSize
            : Math.Min(pageSize, MaximumPageSize);

        eventType = NormalizeFilter(eventType);
        buildVersion = NormalizeFilter(buildVersion);

        IQueryable<GameSessionEvent> query = _dbContext.GameSessionEvents
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(x => x.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(buildVersion))
        {
            query = query.Where(x => x.GameSession.BuildVersion == buildVersion);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtcExclusive.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < toUtcExclusive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (page > totalPages)
        {
            page = totalPages;
        }

        var rawItems = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.GameSession.SessionId,
                InstallationId = x.GameSession.Installation.InstallationId,
                OwnerUserId = x.GameSession.Installation.OwnerUserId,
                DeviceName = x.GameSession.Installation.DeviceName,
                x.GameSession.BuildVersion,
                x.EventType,
                x.Scene,
                x.GameState,
                x.Phase,
                x.PayloadJson,
                x.ClientTimeUtc,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(x => new AdminSessionEventItemDto
            {
                Id = x.Id,
                SessionId = x.SessionId,
                InstallationId = x.InstallationId,
                OwnerUserId = x.OwnerUserId,
                DeviceName = NormalizeDisplayValue(x.DeviceName),
                BuildVersion = NormalizeDisplayValue(x.BuildVersion),
                EventType = NormalizeDisplayValue(x.EventType),
                Scene = NormalizeDisplayValue(x.Scene),
                GameState = NormalizeDisplayValue(x.GameState),
                Phase = NormalizeDisplayValue(x.Phase),
                PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson)
                    ? string.Empty
                    : x.PayloadJson,
                ClientTimeUtc = x.ClientTimeUtc,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        var availableEventTypes = await _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(x => x.EventType != null && x.EventType != string.Empty)
            .Select(x => x.EventType)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var availableBuildVersions = await _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(x =>
                x.GameSession.BuildVersion != null &&
                x.GameSession.BuildVersion != string.Empty)
            .Select(x => x.GameSession.BuildVersion)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Loaded admin session events. EventType={EventType}, BuildVersion={BuildVersion}, Page={Page}, PageSize={PageSize}, TotalCount={TotalCount}.",
            eventType,
            buildVersion,
            page,
            pageSize,
            totalCount);

        return new AdminSessionEventsPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages,
            AvailableEventTypes = availableEventTypes,
            AvailableBuildVersions = availableBuildVersions,
            Items = items
        };
    }

    public async Task<AdminSessionTimelineDetailDto?> GetSessionTimelineAsync(
    Guid sessionId,
    CancellationToken cancellationToken = default)
    {
        var rawSession = await _dbContext.GameSessions
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .Select(x => new
            {
                GameSessionDbId = x.Id,
                x.SessionId,
                InstallationId = x.Installation.InstallationId,
                OwnerUserId = x.Installation.OwnerUserId,
                OwnerAlias = x.Installation.OwnerUser != null
                    ? x.Installation.OwnerUser.Alias
                    : null,
                DeviceName = x.Installation.DeviceName,
                DeviceModel = x.Installation.DeviceModel,
                Platform = x.Installation.Platform,
                OperatingSystem = x.Installation.OSVersion,
                x.BuildVersion,
                x.CurrentScene,
                x.CurrentGameState,
                x.CurrentPhase,
                x.Status,
                x.StartedAtUtc,
                x.LastHeartbeatUtc,
                x.EndedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (rawSession is null)
        {
            _logger.LogWarning(
                "Admin session timeline was not found. SessionId={SessionId}.",
                sessionId);

            return null;
        }

        var rawEvents = await _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(x => x.GameSessionId == rawSession.GameSessionDbId)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.Scene,
                x.GameState,
                x.Phase,
                x.PayloadJson,
                x.ClientTimeUtc,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var endUtc = rawSession.EndedAtUtc ?? rawSession.LastHeartbeatUtc;
        var duration = endUtc - rawSession.StartedAtUtc;

        var durationSeconds = duration > TimeSpan.Zero
            ? (long)Math.Floor(duration.TotalSeconds)
            : 0L;

        var status = (int)rawSession.Status;

        var events = rawEvents
            .Select(x => new AdminSessionTimelineEventDto
            {
                Id = x.Id,
                EventType = NormalizeDisplayValue(x.EventType),
                Scene = NormalizeDisplayValue(x.Scene),
                GameState = NormalizeDisplayValue(x.GameState),
                Phase = NormalizeDisplayValue(x.Phase),
                PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson)
                    ? string.Empty
                    : x.PayloadJson,
                ClientTimeUtc = x.ClientTimeUtc,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        _logger.LogInformation(
            "Loaded admin session timeline. SessionId={SessionId}, EventCount={EventCount}.",
            rawSession.SessionId,
            events.Count);

        return new AdminSessionTimelineDetailDto
        {
            SessionId = rawSession.SessionId,
            InstallationId = rawSession.InstallationId,
            OwnerUserId = rawSession.OwnerUserId,
            OwnerAlias = NormalizeDisplayValue(rawSession.OwnerAlias),
            DeviceName = NormalizeDisplayValue(rawSession.DeviceName),
            DeviceModel = NormalizeDisplayValue(rawSession.DeviceModel),
            Platform = NormalizeDisplayValue(rawSession.Platform),
            OperatingSystem = NormalizeDisplayValue(rawSession.OperatingSystem),
            BuildVersion = NormalizeDisplayValue(rawSession.BuildVersion),
            CurrentScene = NormalizeDisplayValue(rawSession.CurrentScene),
            CurrentGameState = NormalizeDisplayValue(rawSession.CurrentGameState),
            CurrentPhase = NormalizeDisplayValue(rawSession.CurrentPhase),
            Status = status,
            StatusLabel = MapSessionStatusLabel(status),
            StartedAtUtc = rawSession.StartedAtUtc,
            LastHeartbeatUtc = rawSession.LastHeartbeatUtc,
            EndedAtUtc = rawSession.EndedAtUtc,
            DurationSeconds = durationSeconds,
            EventCount = events.Count,
            Events = events
        };
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string NormalizeDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value;
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