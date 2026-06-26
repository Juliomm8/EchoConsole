using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EchoConsole.Api.BackgroundServices;

public sealed class SessionPresenceWorker : BackgroundService
{
    private static readonly TimeSpan WorkerInterval =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan SessionTimeout =
        TimeSpan.FromSeconds(45);

    private const string SimulationDevicePrefix = "PC-Player-";

    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<SessionPresenceWorker> _logger;

    private bool _schemaWarningLogged;

    public SessionPresenceWorker(
        IServiceProvider serviceProvider,
        IHubContext<TelemetryHub> hubContext,
        ILogger<SessionPresenceWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireStaleSessionsAsync(stoppingToken);
                _schemaWarningLogged = false;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SqlException ex)
                when (ex.Number == 207)
            {
                if (!_schemaWarningLogged)
                {
                    _logger.LogWarning(
                        ex,
                        "Session presence cleanup was skipped because the SQL schema is not synchronized. Apply migration AddInstallationAdminMetadata before restarting the API.");

                    _schemaWarningLogged = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Session presence cleanup failed. The worker will retry on the next cycle without stopping the host.");
            }

            try
            {
                await Task.Delay(
                    WorkerInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ExpireStaleSessionsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<EchoConsoleDbContext>();

        var cutoff = DateTimeOffset.UtcNow.Subtract(
            SessionTimeout);

        var candidates = await dbContext.GameSessions
            .AsNoTracking()
            .Where(session =>
                session.Status == SessionStatus.Active &&
                session.EndedAtUtc == null &&
                session.LastHeartbeatUtc < cutoff &&
                !session.Installation.DeviceName.StartsWith(
                    SimulationDevicePrefix))
            .Select(session => new ExpiredSessionCandidate(
                session.Id,
                session.SessionId,
                session.Installation.InstallationId,
                session.LastHeartbeatUtc))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var candidateIds = candidates
            .Select(candidate => candidate.DatabaseId)
            .ToArray();

        await dbContext.GameSessions
            .Where(session =>
                candidateIds.Contains(session.Id) &&
                session.Status == SessionStatus.Active &&
                session.EndedAtUtc == null &&
                session.LastHeartbeatUtc < cutoff)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.Status,
                    SessionStatus.Expired),
                cancellationToken);

        var confirmedExpiredIds = await dbContext.GameSessions
            .AsNoTracking()
            .Where(session =>
                candidateIds.Contains(session.Id) &&
                session.Status == SessionStatus.Expired)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);

        var confirmedExpiredSet = confirmedExpiredIds.ToHashSet();

        foreach (var candidate in candidates)
        {
            if (!confirmedExpiredSet.Contains(candidate.DatabaseId))
            {
                continue;
            }

            await _hubContext.Clients.All.SendAsync(
                "sessionExpired",
                new
                {
                    sessionId = candidate.SessionId,
                    installationId = candidate.InstallationId,
                    lastHeartbeatUtc = candidate.LastHeartbeatUtc
                },
                cancellationToken);
        }
    }

    private sealed record ExpiredSessionCandidate(
        long DatabaseId,
        Guid SessionId,
        Guid InstallationId,
        DateTimeOffset LastHeartbeatUtc);
}
