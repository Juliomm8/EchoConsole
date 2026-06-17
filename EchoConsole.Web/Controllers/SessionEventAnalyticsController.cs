using EchoConsole.Web.Models.Api.SessionEventAnalytics;
using EchoConsole.Web.Models.SessionEventAnalytics;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/SessionEventAnalytics")]
public sealed class SessionEventAnalyticsController : Controller
{
    private readonly EchoConsoleSessionEventAnalyticsApiClient _apiClient;
    private readonly ILogger<SessionEventAnalyticsController> _logger;

    public SessionEventAnalyticsController(
        EchoConsoleSessionEventAnalyticsApiClient apiClient,
        ILogger<SessionEventAnalyticsController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? buildVersion,
        DateTime? fromDate,
        DateTime? toDate,
        string trendGranularity = "day",
        CancellationToken cancellationToken = default)
    {
        trendGranularity = string.Equals(
            trendGranularity,
            "hour",
            StringComparison.OrdinalIgnoreCase)
                ? "hour"
                : "day";

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            ViewData["Title"] = "EVENT ANALYTICS";

            return View(new SessionEventAnalyticsIndexViewModel
            {
                BuildVersion = buildVersion ?? string.Empty,
                FromDate = fromDate,
                ToDate = toDate,
                ErrorMessage = "The start date cannot be later than the end date."
            });
        }

        var fromUtc = fromDate.HasValue
            ? ToUtcStartOfDay(fromDate.Value)
            : (DateTimeOffset?)null;

        var toUtcExclusive = toDate.HasValue
            ? ToUtcStartOfDay(toDate.Value.Date.AddDays(1))
            : (DateTimeOffset?)null;

        var response = await _apiClient.GetAnalyticsAsync(
            buildVersion,
            fromUtc,
            toUtcExclusive,
            trendGranularity,
            cancellationToken);

        if (response is null)
        {
            _logger.LogWarning(
                "Session event analytics could not be loaded.");

            ViewData["Title"] = "EVENT ANALYTICS";

            return View(new SessionEventAnalyticsIndexViewModel
            {
                BuildVersion = buildVersion ?? string.Empty,
                FromDate = fromDate,
                ToDate = toDate,
                ErrorMessage = "The analytics dashboard could not be loaded."
            });
        }

        var model = new SessionEventAnalyticsIndexViewModel
        {
            BuildVersion = buildVersion ?? string.Empty,
            FromDate = fromDate,
            ToDate = toDate,
            TotalEvents = response.TotalEvents,
            EventTypeCount = response.EventTypes.Count,
            SceneCount = response.Scenes.Count,
            BuildCount = response.BuildVersions.Count,
            AvailableBuildVersions = response.AvailableBuildVersions,
            TrendGranularity = response.TrendGranularity,
            TimeSeries = response.TimeSeries
                .Select(x => new SessionEventAnalyticsTimePointViewModel
                {
                    IsoUtc = x.BucketStartUtc.ToUniversalTime().ToString("O"),
                    Label = x.BucketStartUtc
                        .ToUniversalTime()
                        .ToString(
                            response.TrendGranularity == "hour"
                                ? "MMM dd HH:mm"
                                : "MMM dd"),
                    Count = x.Count
                })
                .ToList(),

            EventTypes = MapBuckets(
                response.EventTypes,
                response.TotalEvents),

            Scenes = MapBuckets(
                response.Scenes,
                response.TotalEvents),

            BuildVersions = MapBuckets(
                response.BuildVersions,
                response.TotalEvents)
        };

        ViewData["Title"] = "EVENT ANALYTICS";

        return View(model);
    }

    private static IReadOnlyList<SessionEventAnalyticsBucketViewModel> MapBuckets(
        IReadOnlyList<AdminSessionEventAnalyticsBucketApiModel> buckets,
        int totalEvents)
    {
        return buckets
            .Select(bucket =>
            {
                var percentage = totalEvents <= 0
                    ? 0m
                    : decimal.Round(
                        bucket.Count * 100m / totalEvents,
                        2);

                return new SessionEventAnalyticsBucketViewModel
                {
                    Label = string.IsNullOrWhiteSpace(bucket.Key)
                        ? "Unknown"
                        : bucket.Key,

                    Count = bucket.Count,
                    Percentage = percentage,

                    BarWidthPercentage = bucket.Count <= 0
                        ? 0m
                        : Math.Max(percentage, 2m)
                };
            })
            .ToList();
    }

    private static DateTimeOffset ToUtcStartOfDay(DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(
            date.Date,
            DateTimeKind.Utc);

        return new DateTimeOffset(utcDate);
    }
}