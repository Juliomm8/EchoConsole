using EchoConsole.Api.Contracts.Admin.LiveOperations;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.LiveOperations;

public sealed class LiveOperationsService : ILiveOperationsService
{
    private const int ActiveInstallationWindowSeconds = 75;
    private const int DegradedInstallationWindowMinutes = 15;
    private const int ActiveSessionHeartbeatSeconds = 45;
    private const int InstallationGridLimit = 24;

    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<LiveOperationsService> _logger;

    public LiveOperationsService(
        EchoConsoleDbContext dbContext,
        ILogger<LiveOperationsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<LiveOperationsSnapshotDto> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var activeInstallationCutoff = now.AddSeconds(
            -ActiveInstallationWindowSeconds);

        var degradedInstallationCutoff = now.AddMinutes(
            -DegradedInstallationWindowMinutes);

        var activeSessionCutoff = now.AddSeconds(
            -ActiveSessionHeartbeatSeconds);

        var eventsFiveMinuteCutoff = now.AddMinutes(-5);
        var eventsPreviousFiveMinuteCutoff = now.AddMinutes(-10);
        var eventsFifteenMinuteCutoff = now.AddMinutes(-15);

        var totalInstallations = await _dbContext.Installations
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeInstallations = await _dbContext.Installations
            .AsNoTracking()
            .CountAsync(
                x => x.LastUpdateUtc >= activeInstallationCutoff,
                cancellationToken);

        var degradedInstallations = await _dbContext.Installations
            .AsNoTracking()
            .CountAsync(
                x =>
                    x.LastUpdateUtc < activeInstallationCutoff &&
                    x.LastUpdateUtc >= degradedInstallationCutoff,
                cancellationToken);

        var inactiveInstallations = Math.Max(
            0,
            totalInstallations -
            activeInstallations -
            degradedInstallations);

        var activeSessions = await _dbContext.GameSessions
            .AsNoTracking()
            .CountAsync(
                x =>
                    x.Status == SessionStatus.Active &&
                    x.EndedAtUtc == null &&
                    x.LastHeartbeatUtc >= activeSessionCutoff,
                cancellationToken);

        var eventMetrics = await _dbContext.GameSessionEvents
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc >= eventsFifteenMinuteCutoff)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                EventsLast15Minutes = group.Count(),

                EventsLast5Minutes = group.Sum(
                    x => x.CreatedAtUtc >= eventsFiveMinuteCutoff
                        ? 1
                        : 0),

                PreviousFiveMinuteEvents = group.Sum(
                    x =>
                        x.CreatedAtUtc >= eventsPreviousFiveMinuteCutoff &&
                        x.CreatedAtUtc < eventsFiveMinuteCutoff
                            ? 1
                            : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var eventsLast5Minutes =
            eventMetrics?.EventsLast5Minutes ?? 0;

        var eventsLast15Minutes =
            eventMetrics?.EventsLast15Minutes ?? 0;

        var previousFiveMinuteEvents =
            eventMetrics?.PreviousFiveMinuteEvents ?? 0;

        var alertsLast15Minutes = await _dbContext.SystemAlerts
            .AsNoTracking()
            .CountAsync(
                x => x.CreatedAtUtc >= eventsFifteenMinuteCutoff,
                cancellationToken);

        var unresolvedAlerts = await _dbContext.SystemAlerts
            .AsNoTracking()
            .CountAsync(
                x => !x.IsResolved,
                cancellationToken);

        var alertRatePerMinute = decimal.Round(
            alertsLast15Minutes / 15m,
            2);

        var spike = CalculateEventSpike(
            eventsLast5Minutes,
            previousFiveMinuteEvents);

        var installationRows = await _dbContext.Installations
            .AsNoTracking()
            .OrderByDescending(x => x.LastUpdateUtc)
            .Take(InstallationGridLimit)
            .Select(x => new
            {
                x.InstallationId,
                x.OwnerUserId,
                x.DeviceName,
                x.Platform,
                x.BuildVersion,
                x.LastUpdateUtc,

                LastHeartbeatUtc = x.Sessions
                    .OrderByDescending(session => session.LastHeartbeatUtc)
                    .Select(session =>
                        (DateTimeOffset?)session.LastHeartbeatUtc)
                    .FirstOrDefault(),

                CurrentScene = x.Sessions
                    .OrderByDescending(session => session.LastHeartbeatUtc)
                    .Select(session => session.CurrentScene)
                    .FirstOrDefault(),

                CurrentGameState = x.Sessions
                    .OrderByDescending(session => session.LastHeartbeatUtc)
                    .Select(session => session.CurrentGameState)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var installations = installationRows
            .Select(x => new LiveOperationsInstallationDto
            {
                InstallationId = x.InstallationId,
                OwnerUserId = x.OwnerUserId,

                DeviceName = NormalizeDisplayValue(
                    x.DeviceName),

                Platform = NormalizeDisplayValue(
                    x.Platform),

                BuildVersion = NormalizeDisplayValue(
                    x.BuildVersion),

                OperationalState = ResolveInstallationState(
                    x.LastUpdateUtc,
                    activeInstallationCutoff,
                    degradedInstallationCutoff),

                CurrentScene = NormalizeDisplayValue(
                    x.CurrentScene),

                CurrentGameState = NormalizeDisplayValue(
                    x.CurrentGameState),

                LastUpdateUtc = x.LastUpdateUtc,
                LastHeartbeatUtc = x.LastHeartbeatUtc
            })
            .ToList();

        _logger.LogInformation(
            "Built live operations snapshot. ActiveInstallations={ActiveInstallations}, DegradedInstallations={DegradedInstallations}, InactiveInstallations={InactiveInstallations}, ActiveSessions={ActiveSessions}, EventsLast5Minutes={EventsLast5Minutes}.",
            activeInstallations,
            degradedInstallations,
            inactiveInstallations,
            activeSessions,
            eventsLast5Minutes);

        return new LiveOperationsSnapshotDto
        {
            ServerTimeUtc = now,
            TotalInstallations = totalInstallations,
            ActiveInstallations = activeInstallations,
            DegradedInstallations = degradedInstallations,
            InactiveInstallations = inactiveInstallations,
            ActiveSessions = activeSessions,
            EventsLast5Minutes = eventsLast5Minutes,
            EventsLast15Minutes = eventsLast15Minutes,
            PreviousFiveMinuteEvents = previousFiveMinuteEvents,
            AlertsLast15Minutes = alertsLast15Minutes,
            UnresolvedAlerts = unresolvedAlerts,
            AlertRatePerMinute = alertRatePerMinute,
            EventSpikeState = spike.State,
            EventSpikeMultiplier = spike.Multiplier,
            Installations = installations
        };
    }

    private static string ResolveInstallationState(
        DateTimeOffset lastUpdateUtc,
        DateTimeOffset activeCutoff,
        DateTimeOffset degradedCutoff)
    {
        if (lastUpdateUtc >= activeCutoff)
        {
            return "Active";
        }

        if (lastUpdateUtc >= degradedCutoff)
        {
            return "Degraded";
        }

        return "Inactive";
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
            : value;
    }
}