using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Security;
using EchoConsole.Api.Services.Ownership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Profile;

[ApiController]
[Route("api/profile/installations")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class ProfileInstallationsController : ControllerBase
{
    private readonly IInstallationOwnershipService _installationOwnershipService;

    public ProfileInstallationsController(IInstallationOwnershipService installationOwnershipService)
    {
        _installationOwnershipService = installationOwnershipService;
    }

    [HttpPost("claim")]
    public async Task<IActionResult> ClaimInstallation(
        [FromBody] ClaimInstallationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _installationOwnershipService.ClaimInstallationAsync(
            request.InstallationId,
            request.UserId,
            cancellationToken);

        var response = new ClaimInstallationResponseDto
        {
            Success = result.Status is ClaimInstallationStatus.Success
                or ClaimInstallationStatus.AlreadyOwnedByCurrentUser,
            Message = result.Message,
            InstallationId = result.InstallationId,
            OwnerUserId = result.OwnerUserId
        };

        return result.Status switch
        {
            ClaimInstallationStatus.Success => Ok(response),
            ClaimInstallationStatus.AlreadyOwnedByCurrentUser => Ok(response),
            ClaimInstallationStatus.InstallationNotFound => NotFound(response),
            ClaimInstallationStatus.UserNotFound => NotFound(response),
            ClaimInstallationStatus.AlreadyOwnedByAnotherUser => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}