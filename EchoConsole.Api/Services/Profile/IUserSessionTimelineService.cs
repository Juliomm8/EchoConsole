using EchoConsole.Api.Contracts.Profile;

namespace EchoConsole.Api.Services.Profile;

public interface IUserSessionTimelineService
{
    Task<UserSessionEventPageDto?> GetSessionEventsAsync(
        int userId,
        Guid sessionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}