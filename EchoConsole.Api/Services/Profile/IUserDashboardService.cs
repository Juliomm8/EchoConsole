namespace EchoConsole.Api.Services.Profile;

public interface IUserDashboardService
{
    Task<UserDashboardResult> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default);
}