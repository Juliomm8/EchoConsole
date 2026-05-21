using EchoConsole.Api.Contracts.Profile;

namespace EchoConsole.Api.Services.Profile;

public sealed class UserDashboardResult
{
    public UserDashboardStatus Status { get; init; }

    public UserProfileDto? Profile { get; init; }

    public static UserDashboardResult Success(UserProfileDto profile)
        => new()
        {
            Status = UserDashboardStatus.Success,
            Profile = profile
        };

    public static UserDashboardResult UserNotFound()
        => new()
        {
            Status = UserDashboardStatus.UserNotFound
        };
}