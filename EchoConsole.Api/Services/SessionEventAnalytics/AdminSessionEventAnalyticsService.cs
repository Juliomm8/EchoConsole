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
        buildVersion = NormalizeFilter(buildVersion);
        trendGranularity = NormalizeGranularity(trendGranularity);

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

        var timeSeries = trendGranularity == "hour"
            ? await LoadHourlyTimeSeriesAsync(query, cancellationToken)
            : await LoadDailyTimeSeriesAsync(query, cancellationToken);

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
            "Loaded event analytics. BuildVersion={BuildVersion}, FromUtc={FromUtc}, ToUtcExclusive={ToUtcExclusive}, Granularity={Granularity}, TotalEvents={TotalEvents}.",
            buildVersion,
            fromUtc,
            toUtcExclusive,
            trendGranularity,
            totalEvents);

        return new AdminSessionEventAnalyticsDto
        {
            TotalEvents = totalEvents,
            AppliedBuildVersion = buildVersion ?? string.Empty,
            AppliedFromUtc = fromUtc,
            AppliedToUtcExclusive = toUtcExclusive,
            TrendGranularity = trendGranularity,
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
                .ToList(),

            TimeSeries = timeSeries
        };
    }

    private static async Task<IReadOnlyList<AdminSessionEventTimePointDto>>
        LoadHourlyTimeSeriesAsync(
            IQueryable<GameSessionEvent> query,
            CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(x =>
                EF.Functions.DateDiffHour(
                    TrendAnchorUtc,
                    x.CreatedAtUtc))
            .Select(group => new
            {
                Offset = group.Key,
                Count = group.Count()
            })
            .OrderBy(x => x.Offset)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new AdminSessionEventTimePointDto
            {
                BucketStartUtc = TrendAnchorUtc.AddHours(x.Offset),
                Count = x.Count
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<AdminSessionEventTimePointDto>>
        LoadDailyTimeSeriesAsync(
            IQueryable<GameSessionEvent> query,
            CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(x =>
                EF.Functions.DateDiffDay(
                    TrendAnchorUtc,
                    x.CreatedAtUtc))
            .Select(group => new
            {
                Offset = group.Key,
                Count = group.Count()
            })
            .OrderBy(x => x.Offset)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new AdminSessionEventTimePointDto
            {
                BucketStartUtc = TrendAnchorUtc.AddDays(x.Offset),
                Count = x.Count
            })
            .ToList();
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
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