using System.Text.Json;
using EchoConsole.Api.BackgroundServices;
using EchoConsole.Api.Contracts.Admin.Simulation;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.Simulation;

public sealed class SimulationOrchestratorService
    : ISimulationOrchestratorService
{
    private const string SimulationDevicePrefix = "PC-Player-";
    private const string SimulationSource = "SimulationOrchestrator";
    private const string GameCode = "cosmic-diner";
    private const double RealOwnerBindingProbability = 0.70;

    private static readonly TimeSpan ActiveSessionWindow =
        TimeSpan.FromSeconds(45);

    private static readonly string[] SimulationScenes =
    {
        "Diner_Main",
        "Level_01",
        "Combat_Zone",
        "Kitchen_Hideout"
    };

    private static readonly SimulationBuildSeed[] DefaultSimulationBuilds =
    {
        new(
            "v1.0.0-Stable",
            "Production-ready Cosmic Diner baseline with the complete core investigation loop.",
            120,
            false,
            "Unity 2022.3.62f2"),
        new(
            "v1.1.2-Beta-Testing",
            "Beta telemetry build with network resilience tests and expanded session diagnostics.",
            35,
            false,
            "Unity 2022.3.62f2"),
        new(
            "v2.0.0-RetroUI",
            "Current RetroUI branch with the Echo Console telemetry and CMS integration.",
            7,
            true,
            "Unity 2022.3.62f2")
    };

    private readonly EchoConsoleDbContext _dbContext;
    private readonly SessionTokenService _tokenService;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly TimeProvider _timeProvider;
    private readonly SimulationRuntimeState _runtimeState;
    private readonly SimulationExecutionGate _executionGate;
    private readonly ILogger<SimulationOrchestratorService> _logger;

    public SimulationOrchestratorService(
        EchoConsoleDbContext dbContext,
        SessionTokenService tokenService,
        IHubContext<TelemetryHub> hubContext,
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<SimulationOrchestratorService> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _hubContext = hubContext;
        _timeProvider = timeProvider;
        _runtimeState = SimulationRuntimeState.Shared;
        _executionGate = SimulationExecutionGate.Shared;
        _logger = logger;

        SimulationOrchestratorWorker.EnsureStarted(
            scopeFactory,
            loggerFactory,
            applicationLifetime);
    }

    public async Task<SimulationStatusDto> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now.Subtract(ActiveSessionWindow);
        var runtime = _runtimeState.Read();

        var activeRealSessions = await _dbContext.GameSessions
            .AsNoTracking()
            .CountAsync(
                session =>
                    session.Status == SessionStatus.Active &&
                    session.EndedAtUtc == null &&
                    session.LastHeartbeatUtc >= cutoff &&
                    !session.Installation.DeviceName.StartsWith(
                        SimulationDevicePrefix),
                cancellationToken);

        var activeSimulatedSessions =
            await _dbContext.GameSessions
                .AsNoTracking()
                .CountAsync(
                    session =>
                        session.Status == SessionStatus.Active &&
                        session.EndedAtUtc == null &&
                        session.LastHeartbeatUtc >= cutoff &&
                        session.Installation.DeviceName.StartsWith(
                            SimulationDevicePrefix),
                    cancellationToken);

        var simulatedInstallations =
            await _dbContext.Installations
                .AsNoTracking()
                .CountAsync(
                    installation =>
                        installation.DeviceName.StartsWith(
                            SimulationDevicePrefix),
                    cancellationToken);

        var openSimulationAlerts =
            await _dbContext.SystemAlerts
                .AsNoTracking()
                .CountAsync(
                    alert =>
                        alert.Source == SimulationSource &&
                        !alert.IsResolved,
                    cancellationToken);

        return new SimulationStatusDto
        {
            ActiveRealSessions = activeRealSessions,
            ActiveSimulatedSessions = activeSimulatedSessions,
            SimulatedInstallations = simulatedInstallations,
            OpenSimulationAlerts = openSimulationAlerts,
            IsRunning = runtime.IsRunning,
            TargetActiveSessions = runtime.TargetActiveSessions,
            Phase = runtime.Phase.ToString(),
            Volatility = runtime.Volatility,
            StochasticFlowEnabled =
                runtime.EnableStochasticFlow,
            NextChurnAtUtc = runtime.NextChurnAtUtc,
            ServerTimeUtc = now
        };
    }

    public async Task<SimulationCommandResponse> ReconcileAsync(
        SimulationTargetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Modules);

        await _executionGate.Mutex.WaitAsync(
            cancellationToken);

        try
        {
            var now = _timeProvider.GetUtcNow();
            var runtime = _runtimeState.Configure(request, now);

            if (!request.Modules.Sessions &&
                request.TargetActiveSessions > 0)
            {
                return await CreateCommandResponseAsync(
                    "Sessions module is disabled. The sandbox target was not started.",
                    cancellationToken);
            }

            var buildCatalog =
                await EnsureSimulationBuildCatalogAsync(
                    cancellationToken);

            var activeSessions = await ActiveSimulatedSessionsQuery()
                .Include(session => session.Installation)
                .OrderBy(session => session.StartedAtUtc)
                .ToListAsync(cancellationToken);

            var normalizedBuildAssignments =
                NormalizeActiveSessionBuildAssignments(
                    activeSessions,
                    buildCatalog);

            var createdCount = 0;
            var endedCount = 0;
            var generatedEvents =
                new List<GameSessionEvent>();
            var cleanDepartures =
                new List<GameSession>();

            if (!runtime.IsRunning)
            {
                cleanDepartures.AddRange(activeSessions);
                endedCount = EndSessions(cleanDepartures, now);

                if (ShouldSimulateEvents(request))
                {
                    generatedEvents.AddRange(
                        CreateDepartureEvents(
                            cleanDepartures,
                            [],
                            now));
                }

                _runtimeState.MarkPhase(
                    runtime.Revision,
                    SimulationPhase.Idle);
            }
            else
            {
                var softStartFloor = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        runtime.TargetActiveSessions * 0.10));

                if (activeSessions.Count >
                    runtime.TargetActiveSessions)
                {
                    cleanDepartures.AddRange(
                        activeSessions.Take(
                            activeSessions.Count -
                            runtime.TargetActiveSessions));

                    endedCount = EndSessions(
                        cleanDepartures,
                        now);

                    if (ShouldSimulateEvents(request))
                    {
                        generatedEvents.AddRange(
                            CreateDepartureEvents(
                                cleanDepartures,
                                [],
                                now));
                    }
                }
                else if (activeSessions.Count < softStartFloor)
                {
                    createdCount = await CreateSessionsAsync(
                        softStartFloor - activeSessions.Count,
                        runtime.Modules.Installations,
                        now,
                        activeSessions
                            .Select(session =>
                                session.InstallationDbId)
                            .ToHashSet(),
                        buildCatalog,
                        cancellationToken);

                    if (ShouldSimulateEvents(request))
                    {
                        generatedEvents.AddRange(
                            CreateInitialEventBursts(
                                GetAddedSimulatedSessions(),
                                now));
                    }
                }

                var projectedPopulation =
                    activeSessions.Count +
                    createdCount -
                    endedCount;

                _runtimeState.MarkPhase(
                    runtime.Revision,
                    projectedPopulation >=
                    runtime.TargetActiveSessions
                        ? SimulationPhase.Stable
                        : projectedPopulation <= softStartFloor
                            ? SimulationPhase.SoftStart
                            : SimulationPhase.Ramping);
            }

            if (createdCount > 0 ||
                endedCount > 0 ||
                normalizedBuildAssignments > 0 ||
                generatedEvents.Count > 0 ||
                _dbContext.ChangeTracker.HasChanges())
            {
                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                await BroadcastRefreshAsync(
                    "configureSandbox",
                    createdCount,
                    endedCount,
                    cancellationToken);

                await BroadcastDepartureStatesAsync(
                    cleanDepartures,
                    [],
                    cancellationToken);

                await BroadcastSimulationEventsAsync(
                    generatedEvents,
                    cancellationToken);
            }

            var status = await GetStatusAsync(
                cancellationToken);

            var message = !runtime.IsRunning
                ? $"Sandbox stopped. Ended {endedCount} simulated session(s)."
                : $"Sandbox configured. Soft-start population is {status.ActiveSimulatedSessions}/{runtime.TargetActiveSessions}; volatility is {runtime.Volatility}.";

            return new SimulationCommandResponse
            {
                Message = message,
                Status = status
            };
        }
        finally
        {
            _executionGate.Mutex.Release();
        }
    }

    public async Task<SimulationCommandResponse> PulseAsync(
        SimulationTargetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _runtimeState.Configure(
            request,
            _timeProvider.GetUtcNow());

        await RunCycleAsync(cancellationToken);

        return await CreateCommandResponseAsync(
            "A manual stochastic cycle was executed.",
            cancellationToken);
    }

    public async Task RunCycleAsync(
        CancellationToken cancellationToken)
    {
        await _executionGate.Mutex.WaitAsync(
            cancellationToken);

        try
        {
            var runtime = _runtimeState.Read();

            if (!runtime.IsRunning ||
                !runtime.Modules.Sessions)
            {
                return;
            }

            var profile =
                SimulationVolatilityProfiles.For(
                    runtime.Volatility);

            var now = _timeProvider.GetUtcNow();
            var buildCatalog =
                await EnsureSimulationBuildCatalogAsync(
                    cancellationToken);

            await RefreshSimulatedPresenceAsync(
                now,
                runtime.Modules.Installations,
                cancellationToken);

            var activeSessions = await ActiveSimulatedSessionsQuery()
                .Include(session => session.Installation)
                .OrderBy(session => session.StartedAtUtc)
                .ToListAsync(cancellationToken);

            var normalizedBuildAssignments =
                NormalizeActiveSessionBuildAssignments(
                    activeSessions,
                    buildCatalog);

            var createdCount = 0;
            var endedCount = 0;
            var expiredCount = 0;

            var cleanDepartures =
                new List<GameSession>();
            var abruptDepartures =
                new List<GameSession>();
            var generatedEvents =
                new List<GameSessionEvent>();

            var target = runtime.TargetActiveSessions;

            if (activeSessions.Count < target)
            {
                var missing = target - activeSessions.Count;
                var batchSize = Math.Min(
                    missing,
                    profile.GetRampBatchSize(target));

                createdCount = await CreateSessionsAsync(
                    batchSize,
                    runtime.Modules.Installations,
                    now,
                    activeSessions
                        .Select(session =>
                            session.InstallationDbId)
                        .ToHashSet(),
                    buildCatalog,
                    cancellationToken);
            }
            else if (activeSessions.Count > target)
            {
                var excessivePopulation =
                    activeSessions.Count - target;

                var batchSize = Math.Min(
                    excessivePopulation,
                    profile.GetRampBatchSize(target));

                cleanDepartures.AddRange(
                    activeSessions.Take(batchSize));
            }
            else if (runtime.EnableStochasticFlow)
            {
                var churnIsDue =
                    runtime.NextChurnAtUtc is null ||
                    now >= runtime.NextChurnAtUtc.Value;

                if (churnIsDue && activeSessions.Count > 0)
                {
                    var churnCount =
                        profile.GetChurnCount(
                            activeSessions.Count);

                    var churnCandidates = activeSessions
                        .Take(churnCount)
                        .ToArray();

                    foreach (var session in churnCandidates)
                    {
                        if (Random.Shared.NextDouble() <
                            profile.AbruptDropProbability)
                        {
                            abruptDepartures.Add(session);
                        }
                        else
                        {
                            cleanDepartures.Add(session);
                        }
                    }

                    _runtimeState.ScheduleNextChurn(
                        runtime.Revision,
                        now.Add(profile.NextChurnDelay()));
                }
                else if (Random.Shared.NextDouble() < 0.45)
                {
                    var maximumSwing =
                        profile.GetOscillationLimit(target);

                    var delta = Random.Shared.Next(
                        -maximumSwing,
                        maximumSwing + 1);

                    if (delta > 0)
                    {
                        createdCount =
                            await CreateSessionsAsync(
                                Math.Min(
                                    delta,
                                    profile.GetRampBatchSize(
                                        target)),
                                runtime.Modules.Installations,
                                now,
                                activeSessions
                                    .Select(session =>
                                        session.InstallationDbId)
                                    .ToHashSet(),
                                buildCatalog,
                                cancellationToken);
                    }
                    else if (delta < 0)
                    {
                        cleanDepartures.AddRange(
                            activeSessions.Take(
                                Math.Abs(delta)));
                    }
                }
            }

            cleanDepartures = cleanDepartures
                .DistinctBy(session => session.SessionId)
                .ToList();

            abruptDepartures = abruptDepartures
                .Where(abrupt => cleanDepartures.All(
                    clean =>
                        clean.SessionId != abrupt.SessionId))
                .DistinctBy(session => session.SessionId)
                .ToList();

            endedCount = EndSessions(
                cleanDepartures,
                now);

            expiredCount = ExpireSessions(
                abruptDepartures,
                now);

            if (runtime.SimulateEvents &&
                runtime.Modules.Events)
            {
                generatedEvents.AddRange(
                    CreateInitialEventBursts(
                        GetAddedSimulatedSessions(),
                        now));

                generatedEvents.AddRange(
                    CreateDepartureEvents(
                        cleanDepartures,
                        abruptDepartures,
                        now));

                var maintainedSessions = activeSessions
                    .Where(session =>
                        session.Status == SessionStatus.Active &&
                        session.EndedAtUtc == null)
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(Math.Min(activeSessions.Count, 6))
                    .ToArray();

                generatedEvents.AddRange(
                    CreateOrganicEventBursts(
                        maintainedSessions,
                        now));
            }

            if (runtime.Modules.Alerts &&
                Random.Shared.Next(0, 100) >= 94)
            {
                var alertSession = activeSessions
                    .Where(session =>
                        session.Status == SessionStatus.Active)
                    .OrderBy(_ => Random.Shared.Next())
                    .FirstOrDefault();

                var alertBuildVersion =
                    alertSession?.Installation.BuildVersion ??
                    buildCatalog[0].VersionNumber;

                _dbContext.SystemAlerts.Add(
                    CreateAlert(
                        AlertSeverity.Warning,
                        "NETWORK_DISCONNECT",
                        alertBuildVersion,
                        $"Stochastic sandbox detected an organic telemetry spike on build {alertBuildVersion}.",
                        alertSession?.Installation.DeviceName,
                        now));
            }

            var projectedPopulation =
                activeSessions.Count +
                createdCount -
                endedCount -
                expiredCount;

            var stableTolerance =
                profile.GetOscillationLimit(target);

            _runtimeState.MarkPhase(
                runtime.Revision,
                Math.Abs(projectedPopulation - target) <=
                stableTolerance
                    ? SimulationPhase.Stable
                    : SimulationPhase.Ramping);

            if (createdCount > 0 ||
                endedCount > 0 ||
                expiredCount > 0 ||
                normalizedBuildAssignments > 0 ||
                generatedEvents.Count > 0 ||
                _dbContext.ChangeTracker.HasChanges())
            {
                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                await BroadcastRefreshAsync(
                    "stochasticCycle",
                    createdCount,
                    endedCount + expiredCount,
                    cancellationToken);

                await BroadcastDepartureStatesAsync(
                    cleanDepartures,
                    abruptDepartures,
                    cancellationToken);

                await BroadcastSimulationEventsAsync(
                    generatedEvents,
                    cancellationToken);
            }

            _logger.LogInformation(
                "Sandbox cycle completed. Target={Target}, Projected={Projected}, Created={Created}, Ended={Ended}, Expired={Expired}, Volatility={Volatility}",
                target,
                projectedPopulation,
                createdCount,
                endedCount,
                expiredCount,
                runtime.Volatility);
        }
        finally
        {
            _executionGate.Mutex.Release();
        }
    }

    internal TimeSpan GetNextWorkerDelay()
    {
        var runtime = _runtimeState.Read();

        return runtime.IsRunning
            ? SimulationVolatilityProfiles
                .For(runtime.Volatility)
                .NextTickDelay()
            : TimeSpan.FromSeconds(1);
    }

    public async Task<SimulationCommandResponse>
        InjectCriticalAlertAsync(
            SimulationCommandRequest request,
            CancellationToken cancellationToken)
    {
        if (!request.Modules.Alerts)
        {
            return await CreateCommandResponseAsync(
                "Alerts module is disabled. Critical alarm was not injected.",
                cancellationToken);
        }

        var buildCatalog =
            await EnsureSimulationBuildCatalogAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        var installationContext = await _dbContext.Installations
            .AsNoTracking()
            .Where(
                installation =>
                    installation.DeviceName.StartsWith(
                        SimulationDevicePrefix))
            .OrderByDescending(
                installation =>
                    installation.LastUpdateUtc)
            .Select(
                installation => new
                {
                    installation.DeviceName,
                    installation.BuildVersion
                })
            .FirstOrDefaultAsync(cancellationToken);

        var alertBuildVersion =
            installationContext is not null &&
            buildCatalog.Any(
                build =>
                    string.Equals(
                        build.VersionNumber,
                        installationContext.BuildVersion,
                        StringComparison.OrdinalIgnoreCase))
                ? installationContext.BuildVersion
                : buildCatalog[0].VersionNumber;

        var alert = CreateAlert(
            AlertSeverity.Critical,
            "RENDER_PIPELINE_FAULT",
            alertBuildVersion,
            $"Injected critical network and software anomaly for administrative load testing on build {alertBuildVersion}.",
            installationContext?.DeviceName,
            now);

        _dbContext.SystemAlerts.Add(alert);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync(
            "alertCreated",
            new
            {
                alertId = alert.Id,
                severity = alert.Severity.ToString(),
                alert.Message,
                alert.Source,
                alert.InstallationId,
                alert.CreatedAtUtc
            },
            cancellationToken);

        return await CreateCommandResponseAsync(
            $"Critical simulation alert {alert.Id} was created.",
            cancellationToken);
    }

    public async Task<SimulationCommandResponse> MassDropAsync(
        SimulationCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Modules.Sessions)
        {
            return await CreateCommandResponseAsync(
                "Sessions module is disabled. Mass drop was not executed.",
                cancellationToken);
        }

        await _executionGate.Mutex.WaitAsync(
            cancellationToken);

        try
        {
            var buildCatalog =
                await EnsureSimulationBuildCatalogAsync(
                    cancellationToken);

            var now = _timeProvider.GetUtcNow();

            var droppedCount = await ActiveSimulatedSessionsQuery()
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            session => session.Status,
                            SessionStatus.Expired)
                        .SetProperty(
                            session => session.EndedAtUtc,
                            (DateTimeOffset?)null)
                        .SetProperty(
                            session => session.LastHeartbeatUtc,
                            now.Subtract(ActiveSessionWindow)
                                .Subtract(TimeSpan.FromSeconds(1))),
                    cancellationToken);

            if (request.Modules.Installations)
            {
                await _dbContext.Installations
                    .Where(installation =>
                        installation.DeviceName.StartsWith(
                            SimulationDevicePrefix))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                installation =>
                                    installation.Status,
                                "Degraded"),
                        cancellationToken);
            }

            if (request.Modules.Alerts)
            {
                _dbContext.SystemAlerts.Add(
                    CreateAlert(
                        AlertSeverity.Critical,
                        "NETWORK_DISCONNECT",
                        buildCatalog[0].VersionNumber,
                        $"Injected LAN mass drop expired {droppedCount} simulated session(s).",
                        null,
                        now));

                await _dbContext.SaveChangesAsync(
                    cancellationToken);
            }

            if (droppedCount > 0 || request.Modules.Alerts)
            {
                await BroadcastRefreshAsync(
                    "massDrop",
                    0,
                    droppedCount,
                    cancellationToken);
            }

            return await CreateCommandResponseAsync(
                $"Mass drop complete. Expired {droppedCount} simulated session(s); the configured target will recover them through the stochastic ramp.",
                cancellationToken);
        }
        finally
        {
            _executionGate.Mutex.Release();
        }
    }

    public async Task<SimulationMaintenanceResponse>
        PurgeSimulatedDataAsync(
            CancellationToken cancellationToken)
    {
        await _executionGate.Mutex.WaitAsync(
            cancellationToken);

        try
        {
            _runtimeState.Stop(_timeProvider.GetUtcNow());

            var executionStrategy =
                _dbContext.Database.CreateExecutionStrategy();

            var deletedCounts = await executionStrategy.ExecuteAsync(
                async () =>
                {
                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync(
                            cancellationToken);

                    var simulatedInstallationIds =
                        _dbContext.Installations
                            .Where(installation =>
                                installation.DeviceName.StartsWith(
                                    SimulationDevicePrefix))
                            .Select(installation => installation.Id);

                    var simulatedSessionIds =
                        _dbContext.GameSessions
                            .Where(session =>
                                simulatedInstallationIds.Contains(
                                    session.InstallationDbId))
                            .Select(session => session.Id);

                    var deletedSessionEvents =
                        await _dbContext.GameSessionEvents
                            .Where(sessionEvent =>
                                simulatedSessionIds.Contains(
                                    sessionEvent.GameSessionId))
                            .ExecuteDeleteAsync(cancellationToken);

                    var deletedSessions =
                        await _dbContext.GameSessions
                            .Where(session =>
                                simulatedInstallationIds.Contains(
                                    session.InstallationDbId))
                            .ExecuteDeleteAsync(cancellationToken);

                    var deletedAlerts =
                        await _dbContext.SystemAlerts
                            .Where(alert =>
                                alert.Source == SimulationSource ||
                                (alert.InstallationId != null &&
                                 alert.InstallationId.StartsWith(
                                     SimulationDevicePrefix)))
                            .ExecuteDeleteAsync(cancellationToken);

                    var deletedInstallations =
                        await _dbContext.Installations
                            .Where(installation =>
                                installation.DeviceName.StartsWith(
                                    SimulationDevicePrefix))
                            .ExecuteDeleteAsync(cancellationToken);

                    await transaction.CommitAsync(
                        cancellationToken);

                    return new DeletedTelemetryCounts(
                        deletedSessionEvents,
                        deletedSessions,
                        deletedAlerts,
                        deletedInstallations);
                });

            await BroadcastRefreshAsync(
                "purgeSimulated",
                0,
                deletedCounts.Sessions,
                cancellationToken);

            return new SimulationMaintenanceResponse
            {
                Message =
                    "Simulated telemetry data was purged successfully.",
                DeletedSessionEvents =
                    deletedCounts.SessionEvents,
                DeletedSessions =
                    deletedCounts.Sessions,
                DeletedAlerts =
                    deletedCounts.Alerts,
                DeletedInstallations =
                    deletedCounts.Installations,
                Status = await GetStatusAsync(cancellationToken)
            };
        }
        finally
        {
            _executionGate.Mutex.Release();
        }
    }

    public async Task<SimulationMaintenanceResponse> WipeTelemetryAsync(
        CancellationToken cancellationToken)
    {
        await _executionGate.Mutex.WaitAsync(
            cancellationToken);

        try
        {
            _runtimeState.Stop(_timeProvider.GetUtcNow());

            var executionStrategy =
                _dbContext.Database.CreateExecutionStrategy();

            var deletedCounts = await executionStrategy.ExecuteAsync(
                async () =>
                {
                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync(
                            cancellationToken);

                    var deletedSessionEvents =
                        await _dbContext.GameSessionEvents
                            .ExecuteDeleteAsync(cancellationToken);

                    var deletedSessions =
                        await _dbContext.GameSessions
                            .ExecuteDeleteAsync(cancellationToken);

                    var deletedAlerts =
                        await _dbContext.SystemAlerts
                            .ExecuteDeleteAsync(cancellationToken);

                    var deletedInstallations =
                        await _dbContext.Installations
                            .ExecuteDeleteAsync(cancellationToken);

                    await transaction.CommitAsync(
                        cancellationToken);

                    return new DeletedTelemetryCounts(
                        deletedSessionEvents,
                        deletedSessions,
                        deletedAlerts,
                        deletedInstallations);
                });

            await BroadcastRefreshAsync(
                "wipeTelemetry",
                0,
                deletedCounts.Sessions,
                cancellationToken);

            return new SimulationMaintenanceResponse
            {
                Message =
                    "All telemetry installations, sessions, events and alerts were deleted.",
                DeletedSessionEvents =
                    deletedCounts.SessionEvents,
                DeletedSessions =
                    deletedCounts.Sessions,
                DeletedAlerts =
                    deletedCounts.Alerts,
                DeletedInstallations =
                    deletedCounts.Installations,
                Status = await GetStatusAsync(cancellationToken)
            };
        }
        finally
        {
            _executionGate.Mutex.Release();
        }
    }

    private IQueryable<GameSession> ActiveSimulatedSessionsQuery()
    {
        return _dbContext.GameSessions
            .Where(session =>
                session.Status == SessionStatus.Active &&
                session.EndedAtUtc == null &&
                session.Installation.DeviceName.StartsWith(
                    SimulationDevicePrefix));
    }

    private async Task RefreshSimulatedPresenceAsync(
        DateTimeOffset now,
        bool updateInstallations,
        CancellationToken cancellationToken)
    {
        await ActiveSimulatedSessionsQuery()
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.LastHeartbeatUtc,
                    now),
                cancellationToken);

        if (!updateInstallations)
        {
            return;
        }

        await _dbContext.Installations
            .Where(installation =>
                installation.DeviceName.StartsWith(
                    SimulationDevicePrefix) &&
                installation.Sessions.Any(session =>
                    session.Status == SessionStatus.Active &&
                    session.EndedAtUtc == null))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        installation =>
                            installation.LastUpdateUtc,
                        now)
                    .SetProperty(
                        installation =>
                            installation.Status,
                        "Active"),
                cancellationToken);
    }

    private async Task<int> CreateSessionsAsync(
        int requestedCount,
        bool allowInstallationCreation,
        DateTimeOffset now,
        HashSet<int> unavailableInstallationIds,
        IReadOnlyList<SimulationBuildReference> buildCatalog,
        CancellationToken cancellationToken)
    {
        if (requestedCount <= 0)
        {
            return 0;
        }

        var assignableOwnerIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Status == UserStatus.Active)
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var installations = await _dbContext.Installations
            .Where(installation =>
                installation.DeviceName.StartsWith(
                    SimulationDevicePrefix))
            .OrderBy(installation => installation.Id)
            .ToListAsync(cancellationToken);

        var availableInstallations = installations
            .Where(installation =>
                !unavailableInstallationIds.Contains(
                    installation.Id))
            .Take(requestedCount)
            .ToList();

        foreach (var installation in availableInstallations)
        {
            if (!installation.OwnerUserId.HasValue)
            {
                installation.OwnerUserId =
                    _runtimeState.SelectNextOwner(
                        assignableOwnerIds,
                        RealOwnerBindingProbability);
            }
        }

        var missingCount =
            requestedCount - availableInstallations.Count;

        if (missingCount > 0 && allowInstallationCreation)
        {
            var existingNames = installations
                .Select(installation =>
                    installation.DeviceName)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < missingCount; index++)
            {
                var deviceName =
                    CreateNextDeviceName(existingNames);

                existingNames.Add(deviceName);

                var assignedBuild = buildCatalog[
                    (installations.Count + index) %
                    buildCatalog.Count];

                var installation = new Installation
                {
                    InstallationId = Guid.NewGuid(),
                    GameCode = GameCode,
                    BuildVersion =
                        assignedBuild.VersionNumber,
                    Platform = "Windows",
                    DeviceName = deviceName,
                    DeviceModel = "Echo Simulation Node",
                    OSVersion = "Windows 11 Simulation",
                    Processor = "Virtual CPU",
                    Gpu = "Virtual GPU",
                    RamMb = 16384,
                    Status = "Active",
                    FirstSeenUtc = now,
                    LastUpdateUtc = now,
                    OwnerUserId =
                        _runtimeState.SelectNextOwner(
                            assignableOwnerIds,
                            RealOwnerBindingProbability)
                };

                _dbContext.Installations.Add(installation);
                availableInstallations.Add(installation);
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        var selectedInstallations = availableInstallations
            .Take(requestedCount)
            .ToArray();

        var validBuildVersions = buildCatalog
            .Select(build => build.VersionNumber)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        foreach (var installation in selectedInstallations)
        {
            if (!validBuildVersions.Contains(
                    installation.BuildVersion))
            {
                installation.BuildVersion =
                    SelectBuildForInstallation(
                        installation,
                        buildCatalog)
                    .VersionNumber;
            }

            var scene = SimulationScenes[0];
            var simulatedDurationMinutes =
                Random.Shared.Next(10, 26);

            var startedAtUtc =
                now.AddMinutes(
                    -simulatedDurationMinutes);

            var sessionToken =
                _tokenService.GenerateToken();

            var session = new GameSession
            {
                SessionId = Guid.NewGuid(),
                InstallationDbId = installation.Id,
                Installation = installation,
                SessionTokenHash =
                    _tokenService.HashToken(sessionToken),
                BuildVersion = installation.BuildVersion,
                CurrentScene = scene,
                CurrentGameState = ResolveGameState(scene),
                CurrentPhase = ResolvePhase(scene),
                StartedAtUtc = startedAtUtc,
                LastHeartbeatUtc = now,
                Status = SessionStatus.Active
            };

            _dbContext.GameSessions.Add(session);

            installation.LastUpdateUtc = now;
            installation.Status = "Active";
        }

        return selectedInstallations.Length;
    }

    private static int EndSessions(
        IEnumerable<GameSession> sessions,
        DateTimeOffset now)
    {
        var count = 0;

        foreach (var session in sessions)
        {
            session.Status = SessionStatus.Ended;
            session.EndedAtUtc = now;
            session.LastHeartbeatUtc = now;
            count++;
        }

        return count;
    }

    private static int ExpireSessions(
        IEnumerable<GameSession> sessions,
        DateTimeOffset now)
    {
        var count = 0;
        var expiredHeartbeat = now
            .Subtract(ActiveSessionWindow)
            .Subtract(TimeSpan.FromSeconds(1));

        foreach (var session in sessions)
        {
            session.LastHeartbeatUtc = expiredHeartbeat;
            session.EndedAtUtc = null;
            session.Status = SessionStatus.Expired;
            session.Installation.Status = "Degraded";
            count++;
        }

        return count;
    }

    private IReadOnlyList<GameSessionEvent>
        CreateDepartureEvents(
            IEnumerable<GameSession> cleanDepartures,
            IEnumerable<GameSession> abruptDepartures,
            DateTimeOffset now)
    {
        var createdEvents =
            new List<GameSessionEvent>();

        foreach (var session in cleanDepartures
                     .DistinctBy(item => item.SessionId))
        {
            AddSimulationEvent(
                createdEvents,
                session,
                "SessionEnded",
                session.CurrentScene,
                session.CurrentGameState,
                session.CurrentPhase ?? "Unknown",
                now,
                new
                {
                    source = SimulationSource,
                    reason = "OrganicChurn",
                    terminationMode = "Clean",
                    activeSeconds = Math.Max(
                        0,
                        (int)(now - session.StartedAtUtc)
                            .TotalSeconds)
                });
        }

        foreach (var session in abruptDepartures
                     .DistinctBy(item => item.SessionId))
        {
            AddSimulationEvent(
                createdEvents,
                session,
                "HeartbeatDropped",
                session.CurrentScene,
                session.CurrentGameState,
                session.CurrentPhase ?? "Unknown",
                now,
                new
                {
                    source = SimulationSource,
                    reason = "OrganicChurn",
                    terminationMode = "Abrupt",
                    lastHeartbeatUtc =
                        session.LastHeartbeatUtc,
                    timeoutSeconds =
                        (int)ActiveSessionWindow.TotalSeconds
                });
        }

        return createdEvents;
    }

    private static int UpdateSessions(
        IEnumerable<GameSession> sessions,
        DateTimeOffset now,
        bool updateInstallations)
    {
        var count = 0;

        foreach (var session in sessions)
        {
            var scene =
                SimulationScenes[
                    Random.Shared.Next(
                        SimulationScenes.Length)];

            session.CurrentScene = scene;
            session.CurrentGameState =
                ResolveGameState(scene);
            session.CurrentPhase =
                ResolvePhase(scene);
            session.LastHeartbeatUtc = now;

            if (updateInstallations)
            {
                session.Installation.LastUpdateUtc = now;
                session.Installation.Status = "Active";
            }

            count++;
        }

        return count;
    }

    private async Task<IReadOnlyList<SimulationBuildReference>>
        EnsureSimulationBuildCatalogAsync(
            CancellationToken cancellationToken)
    {
        var existingBuilds =
            await LoadSimulationBuildCatalogAsync(cancellationToken);

        if (existingBuilds.Count > 0)
        {
            return existingBuilds;
        }

        var now = _timeProvider.GetUtcNow();

        var seededBuilds = DefaultSimulationBuilds
            .Select(
                seed => new GameBuild
                {
                    VersionNumber = seed.VersionNumber,
                    ReleaseNotes = seed.ReleaseNotes,
                    ReleaseDateUtc =
                        now.AddDays(-seed.ReleaseAgeDays),
                    IsActive = seed.IsActive,
                    EngineVersion = seed.EngineVersion
                })
            .ToArray();

        _dbContext.GameBuilds.AddRange(seededBuilds);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created {BuildCount} base Cosmic Diner build records for simulation.",
                seededBuilds.Length);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _dbContext.ChangeTracker
                         .Entries<GameBuild>()
                         .Where(entry => entry.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }

            existingBuilds =
                await LoadSimulationBuildCatalogAsync(
                    cancellationToken);

            if (existingBuilds.Count == 0)
            {
                throw;
            }

            return existingBuilds;
        }

        return seededBuilds
            .OrderByDescending(build => build.IsActive)
            .ThenByDescending(build => build.ReleaseDateUtc)
            .Select(
                build => new SimulationBuildReference(
                    build.Id,
                    build.VersionNumber,
                    build.IsActive))
            .ToArray();
    }

    private async Task<IReadOnlyList<SimulationBuildReference>>
        LoadSimulationBuildCatalogAsync(
            CancellationToken cancellationToken)
    {
        return await _dbContext.GameBuilds
            .AsNoTracking()
            .OrderByDescending(build => build.IsActive)
            .ThenByDescending(build => build.ReleaseDateUtc)
            .Take(3)
            .Select(
                build => new SimulationBuildReference(
                    build.Id,
                    build.VersionNumber,
                    build.IsActive))
            .ToListAsync(cancellationToken);
    }

    private static int NormalizeActiveSessionBuildAssignments(
        IReadOnlyCollection<GameSession> sessions,
        IReadOnlyList<SimulationBuildReference> buildCatalog)
    {
        if (sessions.Count == 0 ||
            buildCatalog.Count == 0)
        {
            return 0;
        }

        var validBuildVersions = buildCatalog
            .Select(build => build.VersionNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var changedCount = 0;

        foreach (var session in sessions)
        {
            var installation = session.Installation;

            if (!validBuildVersions.Contains(
                    installation.BuildVersion))
            {
                installation.BuildVersion =
                    SelectBuildForInstallation(
                        installation,
                        buildCatalog)
                        .VersionNumber;

                changedCount++;
            }

            if (!string.Equals(
                    session.BuildVersion,
                    installation.BuildVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                session.BuildVersion =
                    installation.BuildVersion;

                changedCount++;
            }
        }

        return changedCount;
    }

    private static SimulationBuildReference
        SelectBuildForInstallation(
            Installation installation,
            IReadOnlyList<SimulationBuildReference> buildCatalog)
    {
        var stableIndex = installation.Id > 0
            ? installation.Id
            : installation.InstallationId.GetHashCode() &
              int.MaxValue;

        return buildCatalog[
            stableIndex % buildCatalog.Count];
    }

    private static string CreateNextDeviceName(
        HashSet<string> existingNames)
    {
        for (var index = 1; index <= 9999; index++)
        {
            var candidate =
                $"{SimulationDevicePrefix}{index:D4}";

            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{SimulationDevicePrefix}{Guid.NewGuid():N}";
    }

    private static bool ShouldSimulateEvents(
        SimulationTargetRequest request)
    {
        return request.SimulateEvents &&
               request.Modules.Events;
    }

    private IReadOnlyList<GameSession> GetAddedSimulatedSessions()
    {
        return _dbContext.ChangeTracker
            .Entries<GameSession>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .Where(session =>
                session.Installation.DeviceName.StartsWith(
                    SimulationDevicePrefix,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private IReadOnlyList<GameSessionEvent> CreateInitialEventBursts(
        IEnumerable<GameSession> sessions,
        DateTimeOffset now)
    {
        var createdEvents = new List<GameSessionEvent>();

        foreach (var session in sessions)
        {
            var totalDuration = now - session.StartedAtUtc;
            var firstTransitionUtc = session.StartedAtUtc.AddMinutes(1);
            var midpointUtc = session.StartedAtUtc.AddTicks(
                totalDuration.Ticks / 2);
            var lateTransitionUtc = session.StartedAtUtc.AddTicks(
                totalDuration.Ticks * 3 / 4);

            AddSimulationEvent(
                createdEvents,
                session,
                "SessionStart",
                SimulationScenes[0],
                "Loading",
                "Bootstrap",
                session.StartedAtUtc,
                new
                {
                    source = SimulationSource,
                    sessionId = session.SessionId,
                    buildVersion = session.BuildVersion,
                    simulatedDurationMinutes =
                        Math.Max(1, (int)totalDuration.TotalMinutes)
                });

            AddSimulationEvent(
                createdEvents,
                session,
                "SceneChanged",
                SimulationScenes[1],
                ResolveGameState(SimulationScenes[1]),
                ResolvePhase(SimulationScenes[1]),
                firstTransitionUtc,
                new
                {
                    previousScene = SimulationScenes[0],
                    currentScene = SimulationScenes[1],
                    transitionReason = "InitialLoad"
                });

            AddSimulationEvent(
                createdEvents,
                session,
                "TelemetryHeartbeat",
                SimulationScenes[1],
                ResolveGameState(SimulationScenes[1]),
                ResolvePhase(SimulationScenes[1]),
                midpointUtc,
                new
                {
                    sequence = 1,
                    latencyMs = Random.Shared.Next(18, 74),
                    frameRate = Random.Shared.Next(55, 121)
                });

            var finalScene = SimulationScenes[
                Random.Shared.Next(2, SimulationScenes.Length)];

            AddSimulationEvent(
                createdEvents,
                session,
                "SceneChanged",
                finalScene,
                ResolveGameState(finalScene),
                ResolvePhase(finalScene),
                lateTransitionUtc,
                new
                {
                    previousScene = SimulationScenes[1],
                    currentScene = finalScene,
                    transitionReason = "GameplayProgression"
                });

            AddSimulationEvent(
                createdEvents,
                session,
                "TelemetryHeartbeat",
                finalScene,
                ResolveGameState(finalScene),
                ResolvePhase(finalScene),
                now,
                new
                {
                    sequence = 2,
                    latencyMs = Random.Shared.Next(16, 68),
                    frameRate = Random.Shared.Next(58, 121)
                });

            session.CurrentScene = finalScene;
            session.CurrentGameState = ResolveGameState(finalScene);
            session.CurrentPhase = ResolvePhase(finalScene);
            session.LastHeartbeatUtc = now;
        }

        return createdEvents;
    }

    private IReadOnlyList<GameSessionEvent> CreateOrganicEventBursts(
        IEnumerable<GameSession> sessions,
        DateTimeOffset now)
    {
        var createdEvents = new List<GameSessionEvent>();

        foreach (var session in sessions)
        {
            var previousScene = session.CurrentScene;
            var shouldChangeScene = Random.Shared.Next(0, 100) < 62;
            var currentScene = previousScene;

            if (shouldChangeScene)
            {
                currentScene = SelectNextScene(previousScene);
                var transitionUtc = now.AddSeconds(
                    -Random.Shared.Next(2, 10));

                AddSimulationEvent(
                    createdEvents,
                    session,
                    "SceneChanged",
                    currentScene,
                    ResolveGameState(currentScene),
                    ResolvePhase(currentScene),
                    transitionUtc,
                    new
                    {
                        previousScene,
                        currentScene,
                        transitionReason = "OrganicSimulation"
                    });
            }

            AddSimulationEvent(
                createdEvents,
                session,
                "TelemetryHeartbeat",
                currentScene,
                ResolveGameState(currentScene),
                ResolvePhase(currentScene),
                now,
                new
                {
                    latencyMs = Random.Shared.Next(14, 92),
                    frameRate = Random.Shared.Next(48, 121),
                    memoryMb = Random.Shared.Next(3200, 7600),
                    activeMinutes = Math.Max(
                        1,
                        (int)(now - session.StartedAtUtc)
                            .TotalMinutes)
                });

            session.CurrentScene = currentScene;
            session.CurrentGameState = ResolveGameState(currentScene);
            session.CurrentPhase = ResolvePhase(currentScene);
            session.LastHeartbeatUtc = now;
            session.Installation.LastUpdateUtc = now;
            session.Installation.Status = "Active";
        }

        return createdEvents;
    }

    private void AddSimulationEvent(
        ICollection<GameSessionEvent> destination,
        GameSession session,
        string eventType,
        string scene,
        string gameState,
        string phase,
        DateTimeOffset eventTimeUtc,
        object payload)
    {
        var simulationEvent = new GameSessionEvent
        {
            GameSession = session,
            EventType = eventType,
            Scene = scene,
            GameState = gameState,
            Phase = phase,
            PayloadJson = JsonSerializer.Serialize(payload),
            ClientTimeUtc = eventTimeUtc,
            CreatedAtUtc = eventTimeUtc
        };

        _dbContext.GameSessionEvents.Add(simulationEvent);
        destination.Add(simulationEvent);
    }

    private async Task BroadcastSimulationEventsAsync(
        IEnumerable<GameSessionEvent> events,
        CancellationToken cancellationToken)
    {
        var orderedEvents = events
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToArray();

        foreach (var simulationEvent in orderedEvents)
        {
            var session = simulationEvent.GameSession;
            var installation = session.Installation;

            await _hubContext.Clients.All.SendAsync(
                "sessionEventRecorded",
                new
                {
                    sessionId = session.SessionId,
                    installationId = installation.InstallationId,
                    ownerUserId = installation.OwnerUserId,
                    deviceName = installation.DeviceName,
                    buildVersion = session.BuildVersion,
                    eventId = simulationEvent.Id,
                    eventType = simulationEvent.EventType,
                    scene = simulationEvent.Scene,
                    gameState = simulationEvent.GameState,
                    phase = simulationEvent.Phase,
                    payloadJson = simulationEvent.PayloadJson,
                    clientTimeUtc = simulationEvent.ClientTimeUtc,
                    createdAtUtc = simulationEvent.CreatedAtUtc
                },
                cancellationToken);
        }

        if (orderedEvents.Length > 0)
        {
            _logger.LogInformation(
                "Simulation event burst persisted and broadcast. EventCount={EventCount}",
                orderedEvents.Length);
        }
    }

    private async Task BroadcastDepartureStatesAsync(
        IEnumerable<GameSession> cleanDepartures,
        IEnumerable<GameSession> abruptDepartures,
        CancellationToken cancellationToken)
    {
        foreach (var session in cleanDepartures
                     .DistinctBy(item => item.SessionId))
        {
            await _hubContext.Clients.All.SendAsync(
                "sessionEnded",
                new
                {
                    sessionId = session.SessionId,
                    installationId =
                        session.Installation.InstallationId,
                    ownerUserId =
                        session.Installation.OwnerUserId,
                    reason = "OrganicChurn",
                    endedAtUtc = session.EndedAtUtc
                },
                cancellationToken);
        }

        foreach (var session in abruptDepartures
                     .DistinctBy(item => item.SessionId))
        {
            await _hubContext.Clients.All.SendAsync(
                "sessionExpired",
                new
                {
                    sessionId = session.SessionId,
                    installationId =
                        session.Installation.InstallationId,
                    ownerUserId =
                        session.Installation.OwnerUserId,
                    lastHeartbeatUtc =
                        session.LastHeartbeatUtc,
                    reason = "HeartbeatTimeout"
                },
                cancellationToken);
        }
    }

    private static string SelectNextScene(string currentScene)
    {
        var currentIndex = Array.FindIndex(
            SimulationScenes,
            scene => string.Equals(
                scene,
                currentScene,
                StringComparison.OrdinalIgnoreCase));

        if (currentIndex < 0)
        {
            return SimulationScenes[0];
        }

        var nextOffset = Random.Shared.Next(1, SimulationScenes.Length);
        return SimulationScenes[
            (currentIndex + nextOffset) % SimulationScenes.Length];
    }

    private static string ResolveGameState(string scene)
    {
        return scene switch
        {
            "Combat_Zone" => "Combat",
            "Kitchen_Hideout" => "Stealth",
            "Level_01" => "Exploration",
            _ => "DinerOperations"
        };
    }

    private static string ResolvePhase(string scene)
    {
        return scene switch
        {
            "Combat_Zone" => "EnemyEncounter",
            "Kitchen_Hideout" => "Investigation",
            "Level_01" => "LevelProgression",
            _ => "ServiceFloor"
        };
    }

    private static SystemAlert CreateAlert(
        AlertSeverity severity,
        string errorTypeCode,
        string? buildVersion,
        string message,
        string? installationName,
        DateTimeOffset now)
    {
        return new SystemAlert
        {
            Severity = severity,
            ErrorTypeCode = errorTypeCode,
            BuildVersion = buildVersion,
            Message = message,
            Source = SimulationSource,
            InstallationId = installationName,
            CreatedAtUtc = now,
            IsResolved = false
        };
    }

    private async Task<SimulationCommandResponse>
        CreateCommandResponseAsync(
            string message,
            CancellationToken cancellationToken)
    {
        return new SimulationCommandResponse
        {
            Message = message,
            Status = await GetStatusAsync(cancellationToken)
        };
    }

    private async Task BroadcastRefreshAsync(
        string reason,
        int createdCount,
        int endedCount,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.All.SendAsync(
            "liveSessionsChanged",
            new
            {
                reason,
                createdCount,
                endedCount,
                serverTimeUtc = _timeProvider.GetUtcNow()
            },
            cancellationToken);

        _logger.LogInformation(
            "Simulation operation completed. Reason={Reason}, Created={CreatedCount}, Ended={EndedCount}",
            reason,
            createdCount,
            endedCount);
    }
    private sealed record SimulationBuildSeed(
        string VersionNumber,
        string ReleaseNotes,
        int ReleaseAgeDays,
        bool IsActive,
        string EngineVersion);

    private sealed record SimulationBuildReference(
        int Id,
        string VersionNumber,
        bool IsActive);

    private sealed record DeletedTelemetryCounts(
        int SessionEvents,
        int Sessions,
        int Alerts,
        int Installations);
}
