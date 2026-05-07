using EchoConsole.Api.Contracts.Client;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Client;

[ApiController]
[Route("api/client/sessions")]
[EnableRateLimiting("client-ingest")]
public sealed class SessionsController : ControllerBase
{
    private const string ExpectedGameCode = "cosmic-diner";
    private const int HeartbeatIntervalSeconds = 15;
    private const int HeartbeatTimeoutSeconds = 45;

    private readonly EchoConsoleDbContext _db;
    private readonly SessionTokenService _tokenService;
    private readonly IHubContext<TelemetryHub> _hub;

    public SessionsController(
        EchoConsoleDbContext db,
        SessionTokenService tokenService,
        IHubContext<TelemetryHub> hub)
    {
        _db = db;
        _tokenService = tokenService;
        _hub = hub;
    }

    [HttpPost("start")]
    public async Task<ActionResult<StartSessionResponse>> Start(
        [FromBody] StartSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.GameCode, ExpectedGameCode, StringComparison.Ordinal))
        {
            return BadRequest("Invalid gameCode.");
        }

        var installation = await _db.Installations
            .FirstOrDefaultAsync(
                x => x.InstallationId == request.InstallationId && x.GameCode == request.GameCode,
                cancellationToken);

        if (installation is null)
        {
            return NotFound("Installation not registered.");
        }

        var now = DateTimeOffset.UtcNow;
        var token = _tokenService.GenerateToken();

        var session = new GameSession
        {
            SessionId = Guid.NewGuid(),
            InstallationDbId = installation.Id,
            SessionTokenHash = _tokenService.HashToken(token),
            BuildVersion = request.BuildVersion.Trim(),
            CurrentScene = request.CurrentScene.Trim(),
            CurrentGameState = request.CurrentGameState.Trim(),
            CurrentPhase = string.IsNullOrWhiteSpace(request.CurrentPhase) ? null : request.CurrentPhase.Trim(),
            StartedAtUtc = now,
            LastHeartbeatUtc = now,
            Status = SessionStatus.Active
        };

        installation.LastSeenUtc = now;

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("sessionStarted", new
        {
            sessionId = session.SessionId,
            installationId = installation.InstallationId,
            buildVersion = session.BuildVersion,
            currentScene = session.CurrentScene,
            currentGameState = session.CurrentGameState,
            currentPhase = session.CurrentPhase,
            startedAtUtc = session.StartedAtUtc
        }, cancellationToken);

        var response = new StartSessionResponse
        {
            SessionId = session.SessionId,
            SessionToken = token,
            HeartbeatIntervalSeconds = HeartbeatIntervalSeconds,
            HeartbeatTimeoutSeconds = HeartbeatTimeoutSeconds,
            StartedAtUtc = now,
            ServerTimeUtc = now
        };

        return Ok(response);
    }

    [HttpPost("{sessionId:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(
        [FromRoute] Guid sessionId,
        [FromHeader(Name = "X-Session-Token")] string sessionToken,
        [FromBody] HeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Unauthorized("Missing session token.");
        }

        var session = await _db.GameSessions
            .Include(x => x.Installation)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return NotFound("Session not found.");
        }

        if (!_tokenService.Matches(sessionToken, session.SessionTokenHash))
        {
            return Unauthorized("Invalid session token.");
        }

        if (session.Status == SessionStatus.Ended)
        {
            return Conflict("Session already ended.");
        }

        var now = DateTimeOffset.UtcNow;

        session.CurrentScene = request.CurrentScene.Trim();
        session.CurrentGameState = request.CurrentGameState.Trim();
        session.CurrentPhase = string.IsNullOrWhiteSpace(request.CurrentPhase) ? null : request.CurrentPhase.Trim();
        session.LastHeartbeatUtc = now;
        session.Status = SessionStatus.Active;

        session.Installation.LastSeenUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("sessionHeartbeat", new
        {
            sessionId = session.SessionId,
            installationId = session.Installation.InstallationId,
            currentScene = session.CurrentScene,
            currentGameState = session.CurrentGameState,
            currentPhase = session.CurrentPhase,
            lastHeartbeatUtc = session.LastHeartbeatUtc
        }, cancellationToken);

        return Ok(new
        {
            sessionId = session.SessionId,
            status = session.Status.ToString(),
            serverTimeUtc = now
        });
    }

    [HttpPost("{sessionId:guid}/end")]
    public async Task<IActionResult> End(
        [FromRoute] Guid sessionId,
        [FromHeader(Name = "X-Session-Token")] string sessionToken,
        [FromBody] EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Unauthorized("Missing session token.");
        }

        var session = await _db.GameSessions
            .Include(x => x.Installation)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return NotFound("Session not found.");
        }

        if (!_tokenService.Matches(sessionToken, session.SessionTokenHash))
        {
            return Unauthorized("Invalid session token.");
        }

        if (session.Status == SessionStatus.Ended)
        {
            return Ok(new
            {
                sessionId = session.SessionId,
                status = session.Status.ToString(),
                serverTimeUtc = DateTimeOffset.UtcNow
            });
        }

        var now = DateTimeOffset.UtcNow;

        session.CurrentScene = request.CurrentScene.Trim();
        session.CurrentGameState = request.CurrentGameState.Trim();
        session.CurrentPhase = string.IsNullOrWhiteSpace(request.CurrentPhase) ? null : request.CurrentPhase.Trim();
        session.EndedAtUtc = now;
        session.LastHeartbeatUtc = now;
        session.Status = SessionStatus.Ended;

        session.Installation.LastSeenUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("sessionEnded", new
        {
            sessionId = session.SessionId,
            installationId = session.Installation.InstallationId,
            reason = request.Reason,
            endedAtUtc = session.EndedAtUtc
        }, cancellationToken);

        return Ok(new
        {
            sessionId = session.SessionId,
            status = session.Status.ToString(),
            serverTimeUtc = now
        });
    }
}