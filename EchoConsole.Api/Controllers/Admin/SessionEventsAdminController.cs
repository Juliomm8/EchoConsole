using EchoConsole.Api.Contracts.Admin.SessionEvents;
using EchoConsole.Api.Security;
using EchoConsole.Api.Services.SessionEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/session-events")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class SessionEventsAdminController : ControllerBase
{
    private readonly IAdminSessionEventsService _sessionEventsService;

    public SessionEventsAdminController(
        IAdminSessionEventsService sessionEventsService)
    {
        _sessionEventsService = sessionEventsService;
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
}
