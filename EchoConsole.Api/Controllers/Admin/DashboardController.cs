using EchoConsole.Api.Contracts.Dashboard;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/dashboard")]
public sealed class DashboardController : ControllerBase
{
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
            .CountAsync(x =>
                x.Status == SessionStatus.Active &&
                x.EndedAtUtc == null &&
                x.LastHeartbeatUtc >= cutoff,
                cancellationToken);

        var registeredInstallations = await _db.Installations.CountAsync(cancellationToken);

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
            .Include(x => x.Installation)
            .Where(x =>
                x.Status == SessionStatus.Active &&
                x.EndedAtUtc == null &&
                x.LastHeartbeatUtc >= cutoff)
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