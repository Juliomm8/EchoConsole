using EchoConsole.Api.Security;
using EchoConsole.Api.Services.SessionEventAnalytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/session-event-analytics")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class SessionEventAnalyticsAdminController : ControllerBase
{
    private readonly IAdminSessionEventAnalyticsService _analyticsService;

    public SessionEventAnalyticsAdminController(
        IAdminSessionEventAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAnalytics(
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

        var result = await _analyticsService.GetAnalyticsAsync(
            buildVersion,
            fromUtc,
            toUtcExclusive,
            cancellationToken);

        return Ok(result);
    }
}