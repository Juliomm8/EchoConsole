using EchoConsole.Api.Contracts.Admin.SessionEvents;

namespace EchoConsole.Api.Services.SessionEvents;

public interface IAdminSessionEventsService
{
    Task<AdminSessionEventsPageDto> GetRecentEventsAsync(
        string? eventType,
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}