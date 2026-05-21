namespace EchoConsole.Api.Services.Profile;

public interface IUserDashboardService
{
    Task<UserDashboardResult> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default);
}