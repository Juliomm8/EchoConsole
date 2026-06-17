using EchoConsole.Web.Models.SessionEvents;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/SessionEvents")]
public sealed class SessionEventsController : Controller
{
    private readonly EchoConsoleSessionEventsApiClient _apiClient;
    private readonly ILogger<SessionEventsController> _logger;

    public SessionEventsController(
        EchoConsoleSessionEventsApiClient apiClient,
        ILogger<SessionEventsController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? eventType,
        string? buildVersion,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return View(new AdminSessionEventsIndexViewModel
            {
                EventType = eventType ?? string.Empty,
                BuildVersion = buildVersion ?? string.Empty,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                TotalPages = 1,
                ErrorMessage = "The start date cannot be later than the end date."
            });
        }

        DateTimeOffset? fromUtc = fromDate.HasValue
                    ? ToUtcStartOfDay(fromDate.Value)
                    : null;

        DateTimeOffset? toUtcExclusive = toDate.HasValue
            ? ToUtcStartOfDay(toDate.Value.Date.AddDays(1))
            : null;

        var response = await _apiClient.GetRecentEventsAsync(
            eventType,
            buildVersion,
            fromUtc,
            toUtcExclusive,
            page,
            pageSize,
            cancellationToken);

        if (response is null)
        {
            _logger.LogWarning(
                "Admin session events page could not be loaded.");

            return View(new AdminSessionEventsIndexViewModel
            {
                EventType = eventType ?? string.Empty,
                BuildVersion = buildVersion ?? string.Empty,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                TotalPages = 1,
                ErrorMessage = "The event dashboard could not be loaded."
            });
        }

        var model = new AdminSessionEventsIndexViewModel
        {
            EventType = eventType ?? string.Empty,
            BuildVersion = buildVersion ?? string.Empty,
            FromDate = fromDate,
            ToDate = toDate,
            Page = response.Page,
            PageSize = response.PageSize,
            TotalCount = response.TotalCount,
            TotalPages = response.TotalPages,
            HasPreviousPage = response.HasPreviousPage,
            HasNextPage = response.HasNextPage,
            AvailableEventTypes = response.AvailableEventTypes,
            AvailableBuildVersions = response.AvailableBuildVersions,
            Items = response.Items
                .Select(x => new AdminSessionEventRowViewModel
                {
                    Id = x.Id,
                    SessionId = x.SessionId,
                    InstallationId = x.InstallationId,
                    OwnerUserLabel = x.OwnerUserId.HasValue
                        ? x.OwnerUserId.Value.ToString()
                        : "Unclaimed",
                    DeviceName = NormalizeDisplayValue(x.DeviceName),
                    BuildVersion = NormalizeDisplayValue(x.BuildVersion),
                    EventType = NormalizeDisplayValue(x.EventType),
                    Scene = NormalizeDisplayValue(x.Scene),
                    GameState = NormalizeDisplayValue(x.GameState),
                    Phase = NormalizeDisplayValue(x.Phase),
                    PayloadJson = x.PayloadJson ?? string.Empty,
                    HasPayload = !string.IsNullOrWhiteSpace(x.PayloadJson),
                    ClientTimeLabel = x.ClientTimeUtc.HasValue
                        ? FormatUtc(x.ClientTimeUtc.Value)
                        : "Not provided",
                    CreatedAtLabel = FormatUtc(x.CreatedAtUtc)
                })
                .ToList()
        };

        ViewData["Title"] = "RECENT SESSION EVENTS";

        return View(model);
    }

    private static DateTimeOffset ToUtcStartOfDay(DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        return new DateTimeOffset(utcDate);
    }

    private static string NormalizeDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value;
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }
}