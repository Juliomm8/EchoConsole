using EchoConsole.Api.Contracts.Admin.SessionEvents;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.SessionEvents;

public sealed class AdminSessionEventsService : IAdminSessionEventsService
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;

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
}