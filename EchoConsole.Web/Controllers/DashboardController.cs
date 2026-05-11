using EchoConsole.Web.Models.Dashboard;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

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
        try
        {
            var overview = await _dashboardApiClient.GetOverviewAsync(cancellationToken);
            var liveSessions = await _dashboardApiClient.GetLiveSessionsAsync(cancellationToken);

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
                IsLive = true,
                Kpis = new List<KpiCardViewModel>
                {
                    new()
                    {
                        Title = "Registered Installations",
                        Value = overview.RegisteredInstallations.ToString("N0"),
                        Subtitle = "Persisted in SQL Server through the API",
                        DeltaText = "Real data",
                        IsPositiveDelta = true,
                        Accent = "cyan",
                        ValueElementId = "kpi-registered-installations"
                    },
                    new()
                    {
                        Title = "Active Sessions",
                        Value = overview.ActiveSessions.ToString("N0"),
                        Subtitle = "Current active sessions reported by the backend",
                        DeltaText = "Live now",
                        IsPositiveDelta = true,
                        Accent = "magenta",
                        ValueElementId = "kpi-active-sessions"
                    },
                    new()
                    {
                        Title = "Average Active Session Duration",
                        Value = FormatDuration(averageActiveSessionDuration),
                        Subtitle = "Calculated from current live sessions",
                        DeltaText = "Real data",
                        IsPositiveDelta = true,
                        Accent = "cyan",
                        ValueElementId = "kpi-average-duration"
                    },
                    new()
                    {
                        Title = "Most Recent Heartbeat",
                        Value = FormatRelativeAge(latestHeartbeatAge),
                        Subtitle = "Freshest session heartbeat received",
                        DeltaText = "Real data",
                        IsPositiveDelta = true,
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
                        StatusLabel = x.Status?.ToString() ?? "Unknown"
                    })
                    .ToList()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build the live monitoring dashboard from EchoConsole.Api.");

            var fallbackModel = new LiveMonitoringDashboardViewModel
            {
                ServerTimeUtc = DateTime.UtcNow,
                IsLive = false,
                Kpis = new List<KpiCardViewModel>
                {
                    new()
                    {
                        Title = "Registered Installations",
                        Value = "0",
                        Subtitle = "API unavailable",
                        DeltaText = "Unavailable",
                        IsPositiveDelta = false,
                        Accent = "cyan",
                        ValueElementId = "kpi-registered-installations"
                    },
                    new()
                    {
                        Title = "Active Sessions",
                        Value = "0",
                        Subtitle = "API unavailable",
                        DeltaText = "Unavailable",
                        IsPositiveDelta = false,
                        Accent = "magenta",
                        ValueElementId = "kpi-active-sessions"
                    },
                    new()
                    {
                        Title = "Average Active Session Duration",
                        Value = "00:00:00",
                        Subtitle = "API unavailable",
                        DeltaText = "Unavailable",
                        IsPositiveDelta = false,
                        Accent = "cyan",
                        ValueElementId = "kpi-average-duration"
                    },
                    new()
                    {
                        Title = "Most Recent Heartbeat",
                        Value = "--",
                        Subtitle = "API unavailable",
                        DeltaText = "Unavailable",
                        IsPositiveDelta = false,
                        Accent = "magenta",
                        ValueElementId = "kpi-latest-heartbeat"
                    }
                },
                Sessions = Array.Empty<LiveSessionRowViewModel>()
            };

            return View(fallbackModel);
        }
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