using EchoConsole.Api.Contracts.Profile;

namespace EchoConsole.Api.Services.Profile;

public interface IUserDashboardService
{
    Task<UserDashboardResult> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<UserSessionHistoryPageDto?> GetSessionHistoryAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}