using EchoConsole.Api.Security;
using EchoConsole.Api.Services.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Profile;

[ApiController]
[Route("api/profile/dashboard")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class ProfileDashboardController : ControllerBase
{
    private readonly IUserDashboardService _userDashboardService;

    public ProfileDashboardController(IUserDashboardService userDashboardService)
    {
        _userDashboardService = userDashboardService;
    }

    [HttpGet("{userId:int}")]
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
}