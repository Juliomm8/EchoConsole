using EchoConsole.Api.Contracts.Admin.SessionEventAnalytics;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.SessionEventAnalytics;

public sealed class AdminSessionEventAnalyticsService
    : IAdminSessionEventAnalyticsService
{
    private static readonly DateTimeOffset TrendAnchorUtc =
        new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int MaximumFilterLength = 64;
    private const int MaximumSceneBuckets = 12;

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
        string? trendGranularity,
        CancellationToken cancellationToken = default)
    {
        var normalizedBuildVersion = NormalizeFilter(buildVersion);
        var normalizedGranularity = NormalizeGranularity(trendGranularity);

        var query = ApplyFilters(
            _dbContext.GameSessionEvents.AsNoTracking(),
            normalizedBuildVersion,
            fromUtc,
            toUtcExclusive)
            .TagWith("SessionEventAnalytics.ServerAggregates");

        var totalEvents = await query.CountAsync(cancellationToken);

        var eventTypes = await query
            .GroupBy(sessionEvent =>
                sessionEvent.EventType == string.Empty
                    ? "Unknown"
                    : sessionEvent.EventType)
            .Select(group => new AdminSessionEventAnalyticsBucketDto
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key)
            .ToListAsync(cancellationToken);

        var scenes = await query
            .GroupBy(sessionEvent =>
                sessionEvent.Scene == string.Empty
                    ? "Unknown"
                    : sessionEvent.Scene)
            .Select(group => new AdminSessionEventAnalyticsBucketDto
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key)
            .Take(MaximumSceneBuckets)
            .ToListAsync(cancellationToken);

        var buildVersions = await query
            .GroupBy(sessionEvent =>
                sessionEvent.GameSession.BuildVersion == string.Empty
                    ? "Unknown"
                    : sessionEvent.GameSession.BuildVersion)
            .Select(group => new AdminSessionEventAnalyticsBucketDto
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key)
            .ToListAsync(cancellationToken);

        var timeSeries = normalizedGranularity == "hour"
            ? await LoadHourlyTimeSeriesAsync(query, cancellationToken)
            : await LoadDailyTimeSeriesAsync(query, cancellationToken);

        var availableBuildVersions = await _dbContext.GameSessions
            .AsNoTracking()
            .Where(session => session.BuildVersion != string.Empty)
            .Select(session => session.BuildVersion)
            .Distinct()
            .OrderByDescending(version => version)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Loaded SQL-aggregated session event analytics. BuildVersion={BuildVersion}, FromUtc={FromUtc}, ToUtcExclusive={ToUtcExclusive}, Granularity={Granularity}, TotalEvents={TotalEvents}.",
            normalizedBuildVersion,
            fromUtc,
            toUtcExclusive,
            normalizedGranularity,
            totalEvents);

        return new AdminSessionEventAnalyticsDto
        {
            TotalEvents = totalEvents,
            AppliedBuildVersion = normalizedBuildVersion ?? string.Empty,
            AppliedFromUtc = fromUtc,
            AppliedToUtcExclusive = toUtcExclusive,
            TrendGranularity = normalizedGranularity,
            AvailableBuildVersions = availableBuildVersions,
            EventTypes = eventTypes,
            Scenes = scenes,
            BuildVersions = buildVersions,
            TimeSeries = timeSeries
        };
    }

    private static IQueryable<GameSessionEvent> ApplyFilters(
        IQueryable<GameSessionEvent> query,
        string? buildVersion,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(
                sessionEvent => sessionEvent.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtcExclusive.HasValue)
        {
            query = query.Where(
                sessionEvent =>
                    sessionEvent.CreatedAtUtc < toUtcExclusive.Value);
        }

        if (buildVersion is not null)
        {
            query = query.Where(
                sessionEvent =>
                    sessionEvent.GameSession.BuildVersion == buildVersion);
        }

        return query;
    }

    private static async Task<IReadOnlyList<AdminSessionEventTimePointDto>>
        LoadHourlyTimeSeriesAsync(
            IQueryable<GameSessionEvent> query,
            CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(sessionEvent =>
                EF.Functions.DateDiffHour(
                    TrendAnchorUtc,
                    sessionEvent.CreatedAtUtc))
            .Select(group => new
            {
                Offset = group.Key,
                Count = group.Count()
            })
            .OrderBy(row => row.Offset)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new AdminSessionEventTimePointDto
            {
                BucketStartUtc = TrendAnchorUtc.AddHours(row.Offset),
                Count = row.Count
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<AdminSessionEventTimePointDto>>
        LoadDailyTimeSeriesAsync(
            IQueryable<GameSessionEvent> query,
            CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(sessionEvent =>
                EF.Functions.DateDiffDay(
                    TrendAnchorUtc,
                    sessionEvent.CreatedAtUtc))
            .Select(group => new
            {
                Offset = group.Key,
                Count = group.Count()
            })
            .OrderBy(row => row.Offset)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new AdminSessionEventTimePointDto
            {
                BucketStartUtc = TrendAnchorUtc.AddDays(row.Offset),
                Count = row.Count
            })
            .ToArray();
    }

    private static string? NormalizeFilter(string? value)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= MaximumFilterLength
            ? normalized
            : normalized[..MaximumFilterLength];
    }

    private static string NormalizeGranularity(string? value)
    {
        return string.Equals(
            value,
            "hour",
            StringComparison.OrdinalIgnoreCase)
                ? "hour"
                : "day";
    }
}
