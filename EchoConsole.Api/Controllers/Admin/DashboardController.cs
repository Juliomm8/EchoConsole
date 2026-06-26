using EchoConsole.Api.Contracts.Dashboard;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private const string SimulationDevicePrefix = "PC-Player-";

    private readonly EchoConsoleDbContext _db;

    public DashboardController(EchoConsoleDbContext db)
    {
        _db = db;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddSeconds(-45);

        var activeSessions = await _db.GameSessions
            .AsNoTracking()
            .CountAsync(x =>
                x.Status == SessionStatus.Active &&
                x.EndedAtUtc == null &&
                (
                    x.LastHeartbeatUtc >= cutoff ||
                    x.Installation.DeviceName.StartsWith(
                        SimulationDevicePrefix)
                ),
                cancellationToken);

        var registeredInstallations = await _db.Installations
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return Ok(new DashboardOverviewDto
        {
            ActiveSessions = activeSessions,
            RegisteredInstallations = registeredInstallations,
            ServerTimeUtc = now
        });
    }

    [HttpGet("live-sessions")]
    public async Task<ActionResult<List<LiveSessionDto>>> GetLiveSessions(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-45);

        var sessions = await _db.GameSessions
            .AsNoTracking()
            .Where(x =>
                x.Status == SessionStatus.Active &&
                x.EndedAtUtc == null &&
                (
                    x.LastHeartbeatUtc >= cutoff ||
                    x.Installation.DeviceName.StartsWith(
                        SimulationDevicePrefix)
                ))
            .OrderByDescending(x => x.LastHeartbeatUtc)
            .Select(x => new LiveSessionDto
            {
                SessionId = x.SessionId,
                InstallationId = x.Installation.InstallationId,
                BuildVersion = x.BuildVersion,
                CurrentScene = x.CurrentScene,
                CurrentGameState = x.CurrentGameState,
                CurrentPhase = x.CurrentPhase,
                StartedAtUtc = x.StartedAtUtc,
                LastHeartbeatUtc = x.LastHeartbeatUtc,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(sessions);
    }
}