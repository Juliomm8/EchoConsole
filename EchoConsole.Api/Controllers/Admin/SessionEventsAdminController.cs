using EchoConsole.Api.Contracts.Admin.SessionEvents;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using EchoConsole.Api.Services.SessionEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/session-events")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class SessionEventsAdminController : ControllerBase
{
    private readonly IAdminSessionEventsService _sessionEventsService;
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<SessionEventsAdminController> _logger;

    public SessionEventsAdminController(
        IAdminSessionEventsService sessionEventsService,
        EchoConsoleDbContext dbContext,
        ILogger<SessionEventsAdminController> logger)
    {
        _sessionEventsService = sessionEventsService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<AdminSessionEventsPageDto>> GetRecentEvents(
        [FromQuery] string? eventType,
        [FromQuery] string? buildVersion,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtcExclusive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc.HasValue &&
            toUtcExclusive.HasValue &&
            fromUtc.Value >= toUtcExclusive.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than toUtcExclusive."
            });
        }

        var result = await _sessionEventsService.GetRecentEventsAsync(
            eventType,
            buildVersion,
            fromUtc,
            toUtcExclusive,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}/timeline")]
    public async Task<ActionResult<AdminSessionTimelineDetailDto>>
        GetSessionTimeline(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        var result = await _sessionEventsService.GetSessionTimelineAsync(
            sessionId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Session was not found."
            });
        }

        return Ok(result);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<ActionResult<PurgeSessionResultDto>> PurgeSession(
        Guid sessionId,
        [FromQuery] string? eventType,
        [FromQuery] string? buildVersion,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc.HasValue &&
            toUtcExclusive.HasValue &&
            fromUtc.Value >= toUtcExclusive.Value)
        {
            return BadRequest(new
            {
                message = "fromUtc must be earlier than toUtcExclusive."
            });
        }

        var session = await _dbContext.GameSessions
            .FirstOrDefaultAsync(
                item => item.SessionId == sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound(new
            {
                message = "Session was not found."
            });
        }

        var sessionEvents = _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(item => item.GameSessionId == session.Id);

        var deletedEventCount = await sessionEvents.CountAsync(
            cancellationToken);

        IQueryable<GameSessionEvent> matchingEvents =
            sessionEvents;

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalizedEventType = eventType.Trim();

            matchingEvents = matchingEvents.Where(
                item => item.EventType == normalizedEventType);
        }

        if (!string.IsNullOrWhiteSpace(buildVersion))
        {
            var normalizedBuildVersion = buildVersion.Trim();

            matchingEvents = matchingEvents.Where(
                item => item.GameSession.BuildVersion ==
                    normalizedBuildVersion);
        }

        if (fromUtc.HasValue)
        {
            matchingEvents = matchingEvents.Where(
                item => item.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtcExclusive.HasValue)
        {
            matchingEvents = matchingEvents.Where(
                item => item.CreatedAtUtc < toUtcExclusive.Value);
        }

        var deletedMatchingEventCount =
            await matchingEvents.CountAsync(cancellationToken);

        _dbContext.GameSessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var purgedAtUtc = DateTimeOffset.UtcNow;

        _logger.LogWarning(
            "Game session physically purged. SessionId={SessionId}, DeletedEventCount={DeletedEventCount}, DeletedMatchingEventCount={DeletedMatchingEventCount}",
            sessionId,
            deletedEventCount,
            deletedMatchingEventCount);

        return Ok(new PurgeSessionResultDto
        {
            SessionId = sessionId,
            DeletedEventCount = deletedEventCount,
            DeletedMatchingEventCount = deletedMatchingEventCount,
            PurgedAtUtc = purgedAtUtc
        });
    }
}
