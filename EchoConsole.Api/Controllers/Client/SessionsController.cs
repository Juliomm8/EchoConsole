using EchoConsole.Api.Contracts.Client;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using EchoConsole.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Client;

[ApiController]
[Route("api/client/sessions")]
public sealed class SessionsController : ControllerBase
{
    private const string ExpectedGameCode = "cosmic-diner";
    private const int HeartbeatIntervalSeconds = 15;
    private const int HeartbeatTimeoutSeconds = 45;

    private readonly EchoConsoleDbContext _db;
    private readonly SessionTokenService _tokenService;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        EchoConsoleDbContext db,
        SessionTokenService tokenService,
        IHubContext<TelemetryHub> hub,
        ILogger<SessionsController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _hub = hub;
        _logger = logger;
    }

    [EnableRateLimiting("client-ingest")]
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

        installation.LastUpdateUtc = now;

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("sessionStarted", new
        {
            sessionId = session.SessionId,
            installationId = installation.InstallationId,
            ownerUserId = installation.OwnerUserId,
            deviceName = installation.DeviceName,
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

    [EnableRateLimiting("client-ingest")]
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

        session.Installation.LastUpdateUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("sessionHeartbeat", new
        {
            sessionId = session.SessionId,
            installationId = session.Installation.InstallationId,
            ownerUserId = session.Installation.OwnerUserId,
            deviceName = session.Installation.DeviceName,
            buildVersion = session.BuildVersion,
            startedAtUtc = session.StartedAtUtc,
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

    [EnableRateLimiting("session-events")]
    [RequestSizeLimit(SessionEventContract.MaxRequestBodyBytes)]
    [Consumes("application/json")]
    [HttpPost("{sessionId:guid}/events")]
    public async Task<ActionResult<CreateSessionEventResponse>> CreateEvent(
        [FromRoute] Guid sessionId,
        [FromHeader(Name = "X-Session-Token")] string sessionToken,
        [FromBody] CreateSessionEventRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Unauthorized(new
            {
                code = "session_token_missing",
                message = "Missing session token."
            });
        }

        if (!SessionEventContract.TryNormalizeEventType(
                request.EventType,
                out var eventType))
        {
            return BadRequest(new
            {
                code = "event_type_invalid",
                message = "The supplied eventType is not part of the official contract.",
                allowedEventTypes = SessionEventContract.OfficialEventTypes
            });
        }

        var payloadValidation =
            SessionEventPayloadValidator.Validate(request.PayloadJson);

        if (!payloadValidation.IsValid)
        {
            return BadRequest(new
            {
                code = payloadValidation.ErrorCode,
                message = payloadValidation.ErrorMessage,
                maxPayloadCharacters =
                    SessionEventContract.MaxPayloadCharacters,
                maxPayloadUtf8Bytes =
                    SessionEventContract.MaxPayloadUtf8Bytes
            });
        }

        var session = await _db.GameSessions
            .Include(x => x.Installation)
            .FirstOrDefaultAsync(
                x => x.SessionId == sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound(new
            {
                code = "session_not_found",
                message = "Session not found."
            });
        }

        if (!_tokenService.Matches(
                sessionToken,
                session.SessionTokenHash))
        {
            return Unauthorized(new
            {
                code = "session_token_invalid",
                message = "Invalid session token."
            });
        }

        if (session.Status != SessionStatus.Active)
        {
            return Conflict(new
            {
                code = "session_not_active",
                message = $"Session is currently {session.Status} and cannot receive new events."
            });
        }

        var now = DateTimeOffset.UtcNow;

        var scene = NormalizeOptional(request.Scene);
        var gameState = NormalizeOptional(request.GameState);
        var phase = NormalizeOptional(request.Phase);

        var sessionEvent = new GameSessionEvent
        {
            GameSessionId = session.Id,
            EventType = eventType,
            Scene = scene,
            GameState = gameState,
            Phase = phase,
            PayloadJson = payloadValidation.NormalizedPayload,
            ClientTimeUtc = request.ClientTimeUtc,
            CreatedAtUtc = now
        };

        if (!string.IsNullOrWhiteSpace(scene))
        {
            session.CurrentScene = scene;
        }

        if (!string.IsNullOrWhiteSpace(gameState))
        {
            session.CurrentGameState = gameState;
        }

        if (!string.IsNullOrWhiteSpace(phase))
        {
            session.CurrentPhase = phase;
        }

        session.LastHeartbeatUtc = now;
        session.Installation.LastUpdateUtc = now;

        _db.GameSessionEvents.Add(sessionEvent);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Session event recorded. SessionId={SessionId}, EventType={EventType}, InstallationId={InstallationId}, PayloadCharacters={PayloadCharacters}.",
            session.SessionId,
            sessionEvent.EventType,
            session.Installation.InstallationId,
            sessionEvent.PayloadJson?.Length ?? 0);

        await _hub.Clients.All.SendAsync(
            "sessionEventRecorded",
            new
            {
                sessionId = session.SessionId,
                installationId =
                    session.Installation.InstallationId,
                ownerUserId =
                    session.Installation.OwnerUserId,
                deviceName =
                    session.Installation.DeviceName,
                buildVersion =
                    session.BuildVersion,
                startedAtUtc =
                    session.StartedAtUtc,
                eventId = sessionEvent.Id,
                eventType = sessionEvent.EventType,
                scene = sessionEvent.Scene,
                gameState = sessionEvent.GameState,
                phase = sessionEvent.Phase,
                payloadJson = sessionEvent.PayloadJson,
                clientTimeUtc = sessionEvent.ClientTimeUtc,
                createdAtUtc = sessionEvent.CreatedAtUtc
            },
            cancellationToken);

        return Ok(new CreateSessionEventResponse
        {
            Id = sessionEvent.Id,
            SessionId = session.SessionId,
            EventType = sessionEvent.EventType,
            Scene = sessionEvent.Scene,
            GameState = sessionEvent.GameState,
            Phase = sessionEvent.Phase,
            CreatedAtUtc = sessionEvent.CreatedAtUtc,
            ServerTimeUtc = now
        });
    }

    [EnableRateLimiting("client-ingest")]
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

        session.Installation.LastUpdateUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("sessionEnded", new
        {
            sessionId = session.SessionId,
            installationId = session.Installation.InstallationId,
            ownerUserId = session.Installation.OwnerUserId,
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}