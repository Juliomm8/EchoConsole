using EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;
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
    public async Task<ActionResult<AdminSessionEventAnalyticsDto>> GetAnalytics(
        [FromQuery] string? buildVersion,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtcExclusive,
        [FromQuery] string trendGranularity = "day",
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

        if (!string.Equals(
                trendGranularity,
                "hour",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                trendGranularity,
                "day",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "trendGranularity must be hour or day."
            });
        }

        var result = await _analyticsService.GetAnalyticsAsync(
            buildVersion,
            fromUtc,
            toUtcExclusive,
            trendGranularity,
            cancellationToken);

        return Ok(result);
    }
}
