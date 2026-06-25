using EchoConsole.Web.Models.SessionEvents;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 50);

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
                    PayloadJson = FormatPayloadJson(x.PayloadJson),
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

    [HttpGet("Timeline/{sessionId:guid}")]
    public async Task<IActionResult> TimelineJson(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetSessionTimelineAsync(
            sessionId,
            cancellationToken);

        return response is null
            ? NotFound(new
            {
                message = "Session timeline was not found."
            })
            : Json(response);
    }

    [HttpDelete("Purge/{sessionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeSession(
        Guid sessionId,
        string? eventType,
        string? buildVersion,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? fromUtc = fromDate.HasValue
            ? ToUtcStartOfDay(fromDate.Value)
            : null;

        DateTimeOffset? toUtcExclusive = toDate.HasValue
            ? ToUtcStartOfDay(toDate.Value.Date.AddDays(1))
            : null;

        var result = await _apiClient.PurgeSessionAsync(
            sessionId,
            eventType,
            buildVersion,
            fromUtc,
            toUtcExclusive,
            cancellationToken);

        if (result is null)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "The session could not be purged."
                });
        }

        return Json(result);
    }

    [HttpGet("Details/{sessionId:guid}")]
    public async Task<IActionResult> Details(
    Guid sessionId,
    CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetSessionTimelineAsync(
            sessionId,
            cancellationToken);

        if (response is null)
        {
            return NotFound();
        }

        var model = new AdminSessionTimelineDetailViewModel
        {
            SessionId = response.SessionId,
            InstallationId = response.InstallationId,

            OwnerLabel = response.OwnerUserId.HasValue
                ? $"{NormalizeDisplayValue(response.OwnerAlias)} · User #{response.OwnerUserId.Value}"
                : "Unclaimed installation",

            DeviceName = NormalizeDisplayValue(response.DeviceName),
            DeviceModel = NormalizeDisplayValue(response.DeviceModel),
            Platform = NormalizeDisplayValue(response.Platform),
            OperatingSystem = NormalizeDisplayValue(response.OperatingSystem),
            BuildVersion = NormalizeDisplayValue(response.BuildVersion),
            CurrentScene = NormalizeDisplayValue(response.CurrentScene),
            CurrentGameState = NormalizeDisplayValue(response.CurrentGameState),
            CurrentPhase = NormalizeDisplayValue(response.CurrentPhase),
            StatusLabel = NormalizeDisplayValue(response.StatusLabel),
            IsLive = response.Status == 1 && !response.EndedAtUtc.HasValue,
            StartedAtLabel = FormatUtc(response.StartedAtUtc),
            LastHeartbeatLabel = FormatUtc(response.LastHeartbeatUtc),
            EndedAtLabel = response.EndedAtUtc.HasValue
                ? FormatUtc(response.EndedAtUtc.Value)
                : "Not ended",
            DurationLabel = FormatDuration(response.DurationSeconds),
            EventCount = response.EventCount,

            Events = response.Events
                .Select((timelineEvent, index) =>
                {
                    var primaryTime = timelineEvent.ClientTimeUtc
                        ?? timelineEvent.CreatedAtUtc;

                    return new AdminSessionTimelineEventViewModel
                    {
                        SequenceNumber = index + 1,
                        Id = timelineEvent.Id,
                        EventType = NormalizeDisplayValue(
                            timelineEvent.EventType),
                        Scene = NormalizeDisplayValue(
                            timelineEvent.Scene),
                        GameState = NormalizeDisplayValue(
                            timelineEvent.GameState),
                        Phase = NormalizeDisplayValue(
                            timelineEvent.Phase),
                        PayloadJson = FormatPayloadJson(
                            timelineEvent.PayloadJson),
                        HasPayload = !string.IsNullOrWhiteSpace(
                            timelineEvent.PayloadJson),
                        PrimaryTimeLabel = FormatUtc(primaryTime),
                        PrimaryTimeSource = timelineEvent.ClientTimeUtc.HasValue
                            ? "Client UTC"
                            : "Server UTC",
                        ServerTimeLabel = FormatUtc(
                            timelineEvent.CreatedAtUtc),
                        ClientTimeLabel = timelineEvent.ClientTimeUtc.HasValue
                            ? FormatUtc(timelineEvent.ClientTimeUtc.Value)
                            : "Not provided"
                    };
                })
                .ToList()
        };

        ViewData["Title"] = "SESSION TIMELINE";

        return View("Details", model);
    }

    private static string FormatDuration(long totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "0 seconds";
        }

        var duration = TimeSpan.FromSeconds(totalSeconds);

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes}m {duration.Seconds}s";
        }

        return $"{duration.Seconds}s";
    }

    private static string FormatPayloadJson(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);

            return JsonSerializer.Serialize(
                document.RootElement,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
        catch (JsonException)
        {
            return payloadJson;
        }
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