using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.BackgroundServices;

public sealed class SessionPresenceWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<TelemetryHub> _hub;

    public SessionPresenceWorker(IServiceProvider serviceProvider, IHubContext<TelemetryHub> hub)
    {
        _serviceProvider = serviceProvider;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EchoConsoleDbContext>();

            var now = DateTimeOffset.UtcNow;
            var cutoff = now.AddSeconds(-45);

            var expiredSessions = await db.GameSessions
                .Include(x => x.Installation)
                .Where(x =>
                    x.Status == SessionStatus.Active &&
                    x.EndedAtUtc == null &&
                    x.LastHeartbeatUtc < cutoff)
                .ToListAsync(stoppingToken);

            if (expiredSessions.Count > 0)
            {
                foreach (var session in expiredSessions)
                {
                    session.Status = SessionStatus.Expired;
                }

                await db.SaveChangesAsync(stoppingToken);

                foreach (var session in expiredSessions)
                {
                    await _hub.Clients.All.SendAsync("sessionExpired", new
                    {
                        sessionId = session.SessionId,
                        installationId = session.Installation.InstallationId,
                        lastHeartbeatUtc = session.LastHeartbeatUtc
                    }, stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}