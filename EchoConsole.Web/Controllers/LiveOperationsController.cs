using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Web.Models.Api.LiveOperations;
using EchoConsole.Web.Models.LiveOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/LiveOperations")]
public sealed class LiveOperationsController : Controller
{
    private const int ActiveSessionHeartbeatSeconds = 45;
    private const int DegradedInstallationWindowMinutes = 15;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LiveOperationsController> _logger;

    public LiveOperationsController(
        IServiceScopeFactory scopeFactory,
        ILogger<LiveOperationsController> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "LIVE OPERATIONS";

        try
        {
            var snapshot = await BuildSnapshotAsync(
                includeFleet: false,
                includeInactiveFleet: false,
                cancellationToken);

            return View(new LiveOperationsIndexViewModel
            {
                IsAvailable = true,
                Snapshot = snapshot
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to build the initial live operations command-center view.");

            return View(new LiveOperationsIndexViewModel
            {
                IsAvailable = false,
                Snapshot = new LiveOperationsSnapshotApiModel
                {
                    ServerTimeUtc = DateTimeOffset.UtcNow
                }
            });
        }
    }

    [HttpGet("Snapshot")]
    public async Task<IActionResult> Snapshot(
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await BuildSnapshotAsync(
                includeFleet: true,
                includeInactiveFleet: true,
                cancellationToken);

            var activeNodes = snapshot.Installations
                .Where(installation =>
                    installation.OperationalState == "Active")
                .ToArray();

            var inactiveFleet = snapshot.Installations
                .Where(installation =>
                    installation.OperationalState != "Active")
                .ToArray();

            return Ok(new
            {
                snapshot.ServerTimeUtc,
                snapshot.TotalInstallations,
                snapshot.ActiveInstallations,
                snapshot.DegradedInstallations,
                snapshot.InactiveInstallations,
                snapshot.ActiveSessions,
                snapshot.EventsLast5Minutes,
                snapshot.EventsLast15Minutes,
                snapshot.PreviousFiveMinuteEvents,
                snapshot.AlertsLast15Minutes,
                snapshot.UnresolvedAlerts,
                snapshot.AlertRatePerMinute,
                snapshot.EventSpikeState,
                snapshot.EventSpikeMultiplier,
                ActiveNodes = activeNodes,
                InactiveFleet = inactiveFleet
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Live operations snapshot generation failed.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Live operations snapshot is unavailable."
                });
        }
    }

    private async Task<LiveOperationsSnapshotApiModel> BuildSnapshotAsync(
        bool includeFleet,
        bool includeInactiveFleet,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var analyticsTask = LoadAnalyticsAsync(
            now,
            cancellationToken);

        var fleetTask = includeFleet
            ? LoadFleetAsync(
                now,
                includeInactiveFleet,
                cancellationToken)
            : Task.FromResult<IReadOnlyList<LiveOperationsInstallationApiModel>>(
                Array.Empty<LiveOperationsInstallationApiModel>());

        await Task.WhenAll(
            analyticsTask,
            fleetTask);

        var analytics = await analyticsTask;
        var fleet = await fleetTask;

        return new LiveOperationsSnapshotApiModel
        {
            ServerTimeUtc = now,
            TotalInstallations = analytics.TotalInstallations,
            ActiveInstallations = analytics.ActiveInstallations,
            DegradedInstallations = analytics.DegradedInstallations,
            InactiveInstallations = analytics.InactiveInstallations,
            ActiveSessions = analytics.ActiveSessions,
            EventsLast5Minutes = analytics.EventsLast5Minutes,
            EventsLast15Minutes = analytics.EventsLast15Minutes,
            PreviousFiveMinuteEvents = analytics.PreviousFiveMinuteEvents,
            AlertsLast15Minutes = analytics.AlertsLast15Minutes,
            UnresolvedAlerts = analytics.UnresolvedAlerts,
            AlertRatePerMinute = analytics.AlertRatePerMinute,
            EventSpikeState = analytics.EventSpikeState,
            EventSpikeMultiplier = analytics.EventSpikeMultiplier,
            Installations = fleet
        };
    }

    private async Task<LiveOperationsAnalytics> LoadAnalyticsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var presenceMetricsTask = LoadPresenceMetricsAsync(
            now,
            cancellationToken);

        var eventMetricsTask = LoadEventMetricsAsync(
            now,
            cancellationToken);

        var alertMetricsTask = LoadAlertMetricsAsync(
            now,
            cancellationToken);

        await Task.WhenAll(
            presenceMetricsTask,
            eventMetricsTask,
            alertMetricsTask);

        var presenceMetrics = await presenceMetricsTask;
        var eventMetrics = await eventMetricsTask;
        var alertMetrics = await alertMetricsTask;

        var eventSpike = CalculateEventSpike(
            eventMetrics.EventsLast5Minutes,
            eventMetrics.PreviousFiveMinuteEvents);

        return new LiveOperationsAnalytics(
            TotalInstallations: presenceMetrics.TotalInstallations,
            ActiveInstallations: presenceMetrics.ActiveInstallations,
            DegradedInstallations: presenceMetrics.DegradedInstallations,
            InactiveInstallations: presenceMetrics.InactiveInstallations,
            ActiveSessions: presenceMetrics.ActiveSessions,
            EventsLast5Minutes: eventMetrics.EventsLast5Minutes,
            EventsLast15Minutes: eventMetrics.EventsLast15Minutes,
            PreviousFiveMinuteEvents: eventMetrics.PreviousFiveMinuteEvents,
            AlertsLast15Minutes: alertMetrics.AlertsLast15Minutes,
            UnresolvedAlerts: alertMetrics.UnresolvedAlerts,
            AlertRatePerMinute: alertMetrics.AlertRatePerMinute,
            EventSpikeState: eventSpike.State,
            EventSpikeMultiplier: eventSpike.Multiplier);
    }

    private async Task<PresenceMetrics> LoadPresenceMetricsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeHeartbeatCutoff = now.AddSeconds(
            -ActiveSessionHeartbeatSeconds);

        var degradedCutoff = now.AddMinutes(
            -DegradedInstallationWindowMinutes);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<EchoConsoleDbContext>();

        var activeSessionsQuery = dbContext.GameSessions
            .AsNoTracking()
            .Where(session =>
                session.Status == SessionStatus.Active &&
                session.EndedAtUtc == null &&
                session.LastHeartbeatUtc >= activeHeartbeatCutoff);

        var activeInstallationIds = await activeSessionsQuery
            .Select(session => session.InstallationDbId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeSessions = await activeSessionsQuery
            .CountAsync(cancellationToken);

        var totalInstallations = await dbContext.Installations
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeInstallations = Math.Min(
            activeInstallationIds.Count,
            totalInstallations);

        IQueryable<EchoConsole.Api.Domain.Entities.Installation>
            degradedInstallationsQuery = dbContext.Installations
                .AsNoTracking()
                .Where(installation =>
                    installation.LastUpdateUtc >= degradedCutoff);

        if (activeInstallationIds.Count > 0)
        {
            degradedInstallationsQuery = degradedInstallationsQuery
                .Where(installation =>
                    !activeInstallationIds.Contains(installation.Id));
        }

        var degradedInstallations = await degradedInstallationsQuery
            .CountAsync(cancellationToken);

        degradedInstallations = Math.Min(
            degradedInstallations,
            Math.Max(
                0,
                totalInstallations - activeInstallations));

        var inactiveInstallations = Math.Max(
            0,
            totalInstallations -
            activeInstallations -
            degradedInstallations);

        return new PresenceMetrics(
            TotalInstallations: totalInstallations,
            ActiveInstallations: activeInstallations,
            DegradedInstallations: degradedInstallations,
            InactiveInstallations: inactiveInstallations,
            ActiveSessions: activeSessions);
    }

    private async Task<EventMetrics> LoadEventMetricsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fiveMinuteCutoff = now.AddMinutes(-5);
        var previousFiveMinuteCutoff = now.AddMinutes(-10);
        var fifteenMinuteCutoff = now.AddMinutes(-15);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<EchoConsoleDbContext>();

        var eventsLast5Minutes = await dbContext.GameSessionEvents
            .AsNoTracking()
            .CountAsync(
                sessionEvent =>
                    sessionEvent.CreatedAtUtc >= fiveMinuteCutoff,
                cancellationToken);

        var previousFiveMinuteEvents = await dbContext.GameSessionEvents
            .AsNoTracking()
            .CountAsync(
                sessionEvent =>
                    sessionEvent.CreatedAtUtc >= previousFiveMinuteCutoff &&
                    sessionEvent.CreatedAtUtc < fiveMinuteCutoff,
                cancellationToken);

        var eventsLast15Minutes = await dbContext.GameSessionEvents
            .AsNoTracking()
            .CountAsync(
                sessionEvent =>
                    sessionEvent.CreatedAtUtc >= fifteenMinuteCutoff,
                cancellationToken);

        return new EventMetrics(
            EventsLast5Minutes: eventsLast5Minutes,
            EventsLast15Minutes: eventsLast15Minutes,
            PreviousFiveMinuteEvents: previousFiveMinuteEvents);
    }

    private async Task<AlertMetrics> LoadAlertMetricsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fifteenMinuteCutoff = now.AddMinutes(-15);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<EchoConsoleDbContext>();

        var alertsLast15Minutes = await dbContext.SystemAlerts
            .AsNoTracking()
            .CountAsync(
                alert =>
                    alert.CreatedAtUtc >= fifteenMinuteCutoff,
                cancellationToken);

        var unresolvedAlerts = await dbContext.SystemAlerts
            .AsNoTracking()
            .CountAsync(
                alert => !alert.IsResolved,
                cancellationToken);

        return new AlertMetrics(
            AlertsLast15Minutes: alertsLast15Minutes,
            UnresolvedAlerts: unresolvedAlerts,
            AlertRatePerMinute: decimal.Round(
                alertsLast15Minutes / 15m,
                2));
    }

    private async Task<IReadOnlyList<LiveOperationsInstallationApiModel>>
        LoadFleetAsync(
            DateTimeOffset now,
            bool includeInactiveFleet,
            CancellationToken cancellationToken)
    {
        var activeHeartbeatCutoff = now.AddSeconds(
            -ActiveSessionHeartbeatSeconds);

        var degradedCutoff = now.AddMinutes(
            -DegradedInstallationWindowMinutes);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<EchoConsoleDbContext>();

        var activeSessionRows = await dbContext.GameSessions
            .AsNoTracking()
            .Where(session =>
                session.Status == SessionStatus.Active &&
                session.EndedAtUtc == null &&
                session.LastHeartbeatUtc >= activeHeartbeatCutoff)
            .OrderByDescending(session => session.LastHeartbeatUtc)
            .ThenByDescending(session => session.Id)
            .Select(session => new SessionTelemetryRow(
                session.Id,
                session.InstallationDbId,
                session.CurrentScene,
                session.CurrentGameState,
                session.LastHeartbeatUtc))
            .ToListAsync(cancellationToken);

        var activeSessionByInstallationId = activeSessionRows
            .GroupBy(session => session.InstallationDbId)
            .ToDictionary(
                group => group.Key,
                group => group.First());

        var activeInstallationIds = activeSessionByInstallationId
            .Keys
            .ToArray();

        var installationQuery = dbContext.Installations
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactiveFleet)
        {
            if (activeInstallationIds.Length == 0)
            {
                return Array.Empty<LiveOperationsInstallationApiModel>();
            }

            installationQuery = installationQuery
                .Where(installation =>
                    activeInstallationIds.Contains(installation.Id));
        }

        var installationRows = await installationQuery
            .Select(installation => new InstallationProjectionRow(
                installation.Id,
                installation.InstallationId,
                installation.OwnerUserId,
                installation.DeviceName,
                installation.Platform,
                installation.BuildVersion,
                installation.LastUpdateUtc))
            .ToListAsync(cancellationToken);

        if (installationRows.Count == 0)
        {
            return Array.Empty<LiveOperationsInstallationApiModel>();
        }

        var latestHeartbeatByInstallationQuery = dbContext.GameSessions
            .AsNoTracking()
            .GroupBy(session => session.InstallationDbId)
            .Select(group => new
            {
                InstallationDbId = group.Key,
                LastHeartbeatUtc = group.Max(
                    session => session.LastHeartbeatUtc)
            });

        if (!includeInactiveFleet)
        {
            latestHeartbeatByInstallationQuery = latestHeartbeatByInstallationQuery
                .Where(row =>
                    activeInstallationIds.Contains(row.InstallationDbId));
        }

        var latestSessionRows = await (
            from session in dbContext.GameSessions.AsNoTracking()
            join latestHeartbeat in latestHeartbeatByInstallationQuery
                on new
                {
                    session.InstallationDbId,
                    session.LastHeartbeatUtc
                }
                equals new
                {
                    latestHeartbeat.InstallationDbId,
                    latestHeartbeat.LastHeartbeatUtc
                }
            select new SessionTelemetryRow(
                session.Id,
                session.InstallationDbId,
                session.CurrentScene,
                session.CurrentGameState,
                session.LastHeartbeatUtc))
            .ToListAsync(cancellationToken);

        var latestSessionByInstallationId = latestSessionRows
            .GroupBy(session => session.InstallationDbId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(session => session.LastHeartbeatUtc)
                    .ThenByDescending(session => session.SessionDbId)
                    .First());

        var fleet = installationRows
            .Select(installation =>
            {
                activeSessionByInstallationId.TryGetValue(
                    installation.InstallationDbId,
                    out var activeSession);

                latestSessionByInstallationId.TryGetValue(
                    installation.InstallationDbId,
                    out var latestSession);

                var displaySession = activeSession ?? latestSession;

                return new LiveOperationsInstallationApiModel
                {
                    InstallationId = installation.InstallationId,
                    OwnerUserId = installation.OwnerUserId,
                    DeviceName = NormalizeDisplayValue(
                        installation.DeviceName),
                    Platform = NormalizeDisplayValue(
                        installation.Platform),
                    BuildVersion = NormalizeDisplayValue(
                        installation.BuildVersion),
                    OperationalState = ResolveOperationalState(
                        activeSession is not null,
                        installation.LastUpdateUtc,
                        degradedCutoff),
                    CurrentScene = NormalizeDisplayValue(
                        displaySession?.CurrentScene),
                    CurrentGameState = NormalizeDisplayValue(
                        displaySession?.CurrentGameState),
                    LastUpdateUtc = installation.LastUpdateUtc,
                    LastHeartbeatUtc = latestSession?.LastHeartbeatUtc
                };
            })
            .OrderBy(installation =>
                OperationalStateSortOrder(
                    installation.OperationalState))
            .ThenByDescending(installation =>
                installation.LastHeartbeatUtc)
            .ThenBy(installation =>
                installation.DeviceName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _logger.LogDebug(
            "Projected live operations fleet with atomic presence resolution. IncludeInactiveFleet={IncludeInactiveFleet}, ActiveRows={ActiveRows}, FleetRows={FleetRows}.",
            includeInactiveFleet,
            activeSessionRows.Count,
            fleet.Length);

        return fleet;
    }

    private static string ResolveOperationalState(
        bool hasActiveSession,
        DateTimeOffset lastUpdateUtc,
        DateTimeOffset degradedCutoff)
    {
        if (hasActiveSession)
        {
            return "Active";
        }

        return lastUpdateUtc >= degradedCutoff
            ? "Degraded"
            : "Inactive";
    }

    private static int OperationalStateSortOrder(string state)
    {
        return state switch
        {
            "Active" => 0,
            "Degraded" => 1,
            _ => 2
        };
    }

    private static (string State, decimal Multiplier) CalculateEventSpike(
        int currentEvents,
        int previousEvents)
    {
        var multiplier = previousEvents > 0
            ? decimal.Round(
                currentEvents / (decimal)previousEvents,
                2)
            : currentEvents;

        if (currentEvents == 0)
        {
            return ("Quiet", 0m);
        }

        if (currentEvents >= 8 &&
            (previousEvents == 0 || multiplier >= 2m))
        {
            return ("Spike", multiplier);
        }

        if (currentEvents >= 3 &&
            (previousEvents == 0 || multiplier >= 1.25m))
        {
            return ("Elevated", multiplier);
        }

        return ("Normal", multiplier);
    }

    private static string NormalizeDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();
    }

    private sealed record InstallationProjectionRow(
        int InstallationDbId,
        Guid InstallationId,
        int? OwnerUserId,
        string DeviceName,
        string Platform,
        string BuildVersion,
        DateTimeOffset LastUpdateUtc);

    private sealed record SessionTelemetryRow(
        long SessionDbId,
        int InstallationDbId,
        string CurrentScene,
        string CurrentGameState,
        DateTimeOffset LastHeartbeatUtc);

    private sealed record PresenceMetrics(
        int TotalInstallations,
        int ActiveInstallations,
        int DegradedInstallations,
        int InactiveInstallations,
        int ActiveSessions);

    private sealed record EventMetrics(
        int EventsLast5Minutes,
        int EventsLast15Minutes,
        int PreviousFiveMinuteEvents);

    private sealed record AlertMetrics(
        int AlertsLast15Minutes,
        int UnresolvedAlerts,
        decimal AlertRatePerMinute);

    private sealed record LiveOperationsAnalytics(
        int TotalInstallations,
        int ActiveInstallations,
        int DegradedInstallations,
        int InactiveInstallations,
        int ActiveSessions,
        int EventsLast5Minutes,
        int EventsLast15Minutes,
        int PreviousFiveMinuteEvents,
        int AlertsLast15Minutes,
        int UnresolvedAlerts,
        decimal AlertRatePerMinute,
        string EventSpikeState,
        decimal EventSpikeMultiplier);
}
