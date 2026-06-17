using EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.SessionEventAnalytics;

public sealed class AdminSessionEventAnalyticsService
    : IAdminSessionEventAnalyticsService
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<AdminSessionEventAnalyticsService> _logger;

    public AdminSessionEventAnalyticsService(
        EchoConsoleDbContext dbContext,
        ILogger<AdminSessionEventAnalyticsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminSessionEventAnalyticsDto> GetAnalyticsAsync(
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        buildVersion = NormalizeFilter(buildVersion);

        IQueryable<GameSessionEvent> query = _dbContext.GameSessionEvents
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(buildVersion))
        {
            query = query.Where(
                x => x.GameSession.BuildVersion == buildVersion);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(
                x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtcExclusive.HasValue)
        {
            query = query.Where(
                x => x.CreatedAtUtc < toUtcExclusive.Value);
        }

        var totalEvents = await query.CountAsync(cancellationToken);

        var eventTypeRows = await query
            .GroupBy(x =>
                string.IsNullOrEmpty(x.EventType)
                    ? "Unknown"
                    : x.EventType)
            .Select(group => new
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        var sceneRows = await query
            .GroupBy(x =>
                string.IsNullOrEmpty(x.Scene)
                    ? "Unknown"
                    : x.Scene)
            .Select(group => new
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        var buildRows = await query
            .GroupBy(x =>
                string.IsNullOrEmpty(x.GameSession.BuildVersion)
                    ? "Unknown"
                    : x.GameSession.BuildVersion)
            .Select(group => new
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        var availableBuildVersions = await _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(x =>
                x.GameSession.BuildVersion != null &&
                x.GameSession.BuildVersion != string.Empty)
            .Select(x => x.GameSession.BuildVersion)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Loaded event analytics. BuildVersion={BuildVersion}, FromUtc={FromUtc}, ToUtcExclusive={ToUtcExclusive}, TotalEvents={TotalEvents}.",
            buildVersion,
            fromUtc,
            toUtcExclusive,
            totalEvents);

        return new AdminSessionEventAnalyticsDto
        {
            TotalEvents = totalEvents,
            AppliedBuildVersion = buildVersion ?? string.Empty,
            AppliedFromUtc = fromUtc,
            AppliedToUtcExclusive = toUtcExclusive,
            AvailableBuildVersions = availableBuildVersions,

            EventTypes = eventTypeRows
                .Select(x => new AdminSessionEventAnalyticsBucketDto
                {
                    Key = x.Key,
                    Count = x.Count
                })
                .ToList(),

            Scenes = sceneRows
                .Select(x => new AdminSessionEventAnalyticsBucketDto
                {
                    Key = x.Key,
                    Count = x.Count
                })
                .ToList(),

            BuildVersions = buildRows
                .Select(x => new AdminSessionEventAnalyticsBucketDto
                {
                    Key = x.Key,
                    Count = x.Count
                })
                .ToList()
        };
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}