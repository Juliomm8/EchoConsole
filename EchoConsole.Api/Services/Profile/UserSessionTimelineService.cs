using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.Profile;

public sealed class UserSessionTimelineService : IUserSessionTimelineService
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 50;

    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<UserSessionTimelineService> _logger;

    public UserSessionTimelineService(
        EchoConsoleDbContext dbContext,
        ILogger<UserSessionTimelineService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserSessionEventPageDto?> GetSessionEventsAsync(
        int userId,
        Guid sessionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1
            ? DefaultPageSize
            : Math.Min(pageSize, MaximumPageSize);

        var ownedSession = await _dbContext.GameSessions
            .AsNoTracking()
            .Where(x =>
                x.SessionId == sessionId &&
                x.Installation.OwnerUserId == userId)
            .Select(x => new
            {
                x.Id,
                x.SessionId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ownedSession is null)
        {
            _logger.LogWarning(
                "Session events were denied or not found. UserId={UserId}, SessionId={SessionId}.",
                userId,
                sessionId);

            return null;
        }

        var eventsQuery = _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(x => x.GameSessionId == ownedSession.Id);

        var totalCount = await eventsQuery
            .CountAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (page > totalPages)
        {
            page = totalPages;
        }

        var skip = (page - 1) * pageSize;

        var rawItems = await eventsQuery
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
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

        var items = rawItems
            .Select(x => new UserSessionEventDto
            {
                Id = x.Id,
                EventType = string.IsNullOrWhiteSpace(x.EventType)
                    ? "-"
                    : x.EventType,

                Scene = string.IsNullOrWhiteSpace(x.Scene)
                    ? "-"
                    : x.Scene,

                GameState = string.IsNullOrWhiteSpace(x.GameState)
                    ? "-"
                    : x.GameState,

                Phase = string.IsNullOrWhiteSpace(x.Phase)
                    ? "-"
                    : x.Phase,

                PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson)
                    ? string.Empty
                    : x.PayloadJson,

                ClientTimeUtc = x.ClientTimeUtc,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        _logger.LogInformation(
            "Loaded session timeline page. UserId={UserId}, SessionId={SessionId}, Page={Page}, PageSize={PageSize}, TotalCount={TotalCount}.",
            userId,
            sessionId,
            page,
            pageSize,
            totalCount);

        return new UserSessionEventPageDto
        {
            SessionId = ownedSession.SessionId,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages,
            Items = items
        };
    }
}