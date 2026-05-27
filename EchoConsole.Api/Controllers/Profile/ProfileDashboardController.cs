using EchoConsole.Api.Security;
using EchoConsole.Api.Services.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Profile;

[ApiController]
[Route("api/profile")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class ProfileDashboardController : ControllerBase
{
    private readonly IUserDashboardService _userDashboardService;

    public ProfileDashboardController(IUserDashboardService userDashboardService)
    {
        _userDashboardService = userDashboardService;
    }

    [HttpGet("dashboard/{userId:int}")]
    public async Task<IActionResult> GetProfile(
        int userId,
        CancellationToken cancellationToken)
    {
        var result = await _userDashboardService.GetProfileAsync(userId, cancellationToken);

        return result.Status switch
        {
            UserDashboardStatus.Success => Ok(result.Profile),
            UserDashboardStatus.UserNotFound => NotFound(new { message = "User was not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unexpected error." })
        };
    }

    [HttpGet("sessions/{userId:int}")]
    public async Task<IActionResult> GetSessionHistory(
        int userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        var result = await _userDashboardService.GetSessionHistoryAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        return Ok(result);
    }

    [HttpGet("sessions/{userId:int}/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionDetail(
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _userDashboardService.GetSessionDetailAsync(
            userId,
            sessionId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "Session was not found for this user." });
        }

        return Ok(result);
    }
}