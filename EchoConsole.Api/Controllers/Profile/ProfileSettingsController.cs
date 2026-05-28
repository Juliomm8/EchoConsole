using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Security;
using EchoConsole.Api.Services.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Profile;

[ApiController]
[Route("api/profile/settings")]
[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
public sealed class ProfileSettingsController : ControllerBase
{
    private readonly IUserProfileSettingsService _profileSettingsService;

    public ProfileSettingsController(IUserProfileSettingsService profileSettingsService)
    {
        _profileSettingsService = profileSettingsService;
    }

    [HttpPut("{userId:int}")]
    public async Task<IActionResult> UpdateProfile(
        int userId,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileSettingsService.UpdateProfileAsync(
            userId,
            request,
            cancellationToken);

        var response = new UpdateProfileResponseDto
        {
            Success = result.Status == UpdateProfileStatus.Success,
            Message = result.Message,
            UserId = result.UserId,
            Alias = result.Alias,
            AvatarKey = result.AvatarKey,
            Theme = result.Theme
        };

        return result.Status switch
        {
            UpdateProfileStatus.Success => Ok(response),
            UpdateProfileStatus.UserNotFound => NotFound(response),
            UpdateProfileStatus.AliasAlreadyTaken => Conflict(response),
            UpdateProfileStatus.AliasRequired => BadRequest(response),
            UpdateProfileStatus.AliasInvalidLength => BadRequest(response),
            UpdateProfileStatus.AliasInvalidCharacters => BadRequest(response),
            UpdateProfileStatus.InvalidAvatarKey => BadRequest(response),
            UpdateProfileStatus.InvalidTheme => BadRequest(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}