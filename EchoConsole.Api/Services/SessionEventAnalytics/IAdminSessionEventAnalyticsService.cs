using EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;

namespace EchoConsole.Api.Services.SessionEventAnalytics;

public interface IAdminSessionEventAnalyticsService
{
    Task<AdminSessionEventAnalyticsDto> GetAnalyticsAsync(
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        string? trendGranularity,
        CancellationToken cancellationToken = default);
}