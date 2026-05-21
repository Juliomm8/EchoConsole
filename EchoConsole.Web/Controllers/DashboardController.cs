using EchoConsole.Web.Models.Dashboard;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class DashboardController : Controller
{
    private readonly EchoConsoleDashboardApiClient _dashboardApiClient;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        EchoConsoleDashboardApiClient dashboardApiClient,
        ILogger<DashboardController> logger)
    {
        _dashboardApiClient = dashboardApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        DashboardOverviewApiDto overview = new();
        IReadOnlyList<LiveSessionApiDto> liveSessions = Array.Empty<LiveSessionApiDto>();

        var overviewLoaded = false;
        var sessionsLoaded = false;

        try
        {
            overview = await _dashboardApiClient.GetOverviewAsync(cancellationToken);
            overviewLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard overview.");
        }

        try
        {
            liveSessions = await _dashboardApiClient.GetLiveSessionsAsync(cancellationToken);
            sessionsLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load live sessions.");
        }

        var nowUtc = overview.ServerTimeUtc == default
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(overview.ServerTimeUtc, DateTimeKind.Utc);

        var activeDurations = liveSessions
            .Where(x => x.StartedAtUtc != default)
            .Select(x => nowUtc - DateTime.SpecifyKind(x.StartedAtUtc, DateTimeKind.Utc))
            .Where(x => x > TimeSpan.Zero)
            .ToList();

        var averageActiveSessionDuration = activeDurations.Count > 0
            ? TimeSpan.FromTicks(Convert.ToInt64(activeDurations.Average(x => x.Ticks)))
            : TimeSpan.Zero;

        var latestHeartbeatAge = liveSessions.Count > 0
            ? liveSessions
                .Select(x => nowUtc - DateTime.SpecifyKind(x.LastHeartbeatUtc, DateTimeKind.Utc))
                .Where(x => x >= TimeSpan.Zero)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Min()
            : TimeSpan.Zero;

        var model = new LiveMonitoringDashboardViewModel
        {
            ServerTimeUtc = nowUtc,
            IsLive = overviewLoaded || sessionsLoaded,
            Kpis = new List<KpiCardViewModel>
            {
                new()
                {
                    Title = "Registered Installations",
                    Value = overview.RegisteredInstallations.ToString("N0"),
                    Subtitle = overviewLoaded ? "Persisted in SQL Server through the API" : "Overview unavailable",
                    DeltaText = overviewLoaded ? "Real data" : "Unavailable",
                    IsPositiveDelta = overviewLoaded,
                    Accent = "cyan",
                    ValueElementId = "kpi-registered-installations"
                },
                new()
                {
                    Title = "Active Sessions",
                    Value = overview.ActiveSessions.ToString("N0"),
                    Subtitle = overviewLoaded ? "Current active sessions reported by the backend" : "Overview unavailable",
                    DeltaText = overviewLoaded ? "Live now" : "Unavailable",
                    IsPositiveDelta = overviewLoaded,
                    Accent = "magenta",
                    ValueElementId = "kpi-active-sessions"
                },
                new()
                {
                    Title = "Average Active Session Duration",
                    Value = FormatDuration(averageActiveSessionDuration),
                    Subtitle = sessionsLoaded ? "Calculated from current live sessions" : "Sessions unavailable",
                    DeltaText = sessionsLoaded ? "Real data" : "Unavailable",
                    IsPositiveDelta = sessionsLoaded,
                    Accent = "cyan",
                    ValueElementId = "kpi-average-duration"
                },
                new()
                {
                    Title = "Most Recent Heartbeat",
                    Value = sessionsLoaded ? FormatRelativeAge(latestHeartbeatAge) : "--",
                    Subtitle = sessionsLoaded ? "Freshest session heartbeat received" : "Sessions unavailable",
                    DeltaText = sessionsLoaded ? "Real data" : "Unavailable",
                    IsPositiveDelta = sessionsLoaded,
                    Accent = "magenta",
                    ValueElementId = "kpi-latest-heartbeat"
                }
            },
            Sessions = liveSessions
                .OrderByDescending(x => x.LastHeartbeatUtc)
                .Select(x => new LiveSessionRowViewModel
                {
                    SessionId = x.SessionId.ToString().ToUpperInvariant(),
                    InstallationId = x.InstallationId.ToString().ToUpperInvariant(),
                    CurrentScene = string.IsNullOrWhiteSpace(x.CurrentScene) ? "-" : x.CurrentScene,
                    GameState = string.IsNullOrWhiteSpace(x.CurrentGameState) ? "-" : x.CurrentGameState,
                    CurrentPhase = string.IsNullOrWhiteSpace(x.CurrentPhase) ? "-" : x.CurrentPhase!,
                    LastHeartbeatLabel = FormatRelativeAge(nowUtc - DateTime.SpecifyKind(x.LastHeartbeatUtc, DateTimeKind.Utc)),
                    StatusLabel = MapSessionStatus(x.Status)
                })
                .ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        try
        {
            var overview = await _dashboardApiClient.GetOverviewAsync(cancellationToken);
            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy dashboard overview from API.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Dashboard overview is temporarily unavailable."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> LiveSessions(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _dashboardApiClient.GetLiveSessionsAsync(cancellationToken);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy live sessions from API.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Live sessions are temporarily unavailable."
            });
        }
    }

    private static string MapSessionStatus(int status)
    {
        return status switch
        {
            1 => "Active",
            2 => "Ended",
            3 => "Expired",
            _ => $"Unknown ({status})"
        };
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    private static string FormatRelativeAge(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalSeconds < 60)
        {
            return $"{Math.Max(0, (int)value.TotalSeconds)}s ago";
        }

        if (value.TotalMinutes < 60)
        {
            return $"{Math.Max(0, (int)value.TotalMinutes)}m ago";
        }

        if (value.TotalHours < 24)
        {
            return $"{Math.Max(0, (int)value.TotalHours)}h ago";
        }

        return $"{Math.Max(0, (int)value.TotalDays)}d ago";
    }
}