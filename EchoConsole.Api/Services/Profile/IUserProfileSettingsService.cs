using EchoConsole.Api.Contracts.Profile;

namespace EchoConsole.Api.Services.Profile;

public interface IUserProfileSettingsService
{
    Task<UpdateProfileResult> UpdateProfileAsync(
        int userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}