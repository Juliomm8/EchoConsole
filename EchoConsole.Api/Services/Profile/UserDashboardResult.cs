using EchoConsole.Api.Contracts.Profile;

namespace EchoConsole.Api.Services.Profile;

public sealed class UserDashboardResult
{
    public UserDashboardStatus Status { get; init; }

    public UserDashboardDto? Dashboard { get; init; }

    public static UserDashboardResult Success(UserDashboardDto dashboard)
        => new()
        {
            Status = UserDashboardStatus.Success,
            Dashboard = dashboard
        };

    public static UserDashboardResult UserNotFound()
        => new()
        {
            Status = UserDashboardStatus.UserNotFound
        };
}