using EchoConsole.Api.Contracts.Client;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace EchoConsole.Api.Controllers.Client;

[ApiController]
[Route("api/client/installations")]
[EnableRateLimiting("client-ingest")]
public sealed class InstallationsController : ControllerBase
{
    private const string ExpectedGameCode = "cosmic-diner";

    private readonly EchoConsoleDbContext _db;
    private readonly IHubContext<TelemetryHub> _hub;

    public InstallationsController(EchoConsoleDbContext db, IHubContext<TelemetryHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterInstallationResponse>> Register(
        [FromBody] RegisterInstallationRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.GameCode, ExpectedGameCode, StringComparison.Ordinal))
        {
            return BadRequest("Invalid gameCode.");
        }

        var now = DateTimeOffset.UtcNow;

        var installation = await _db.Installations
            .FirstOrDefaultAsync(x => x.InstallationId == request.InstallationId, cancellationToken);

        if (installation is null)
        {
            installation = new Installation
            {
                InstallationId = request.InstallationId,
                GameCode = request.GameCode.Trim(),
                BuildVersion = request.BuildVersion.Trim(),
                Platform = request.Platform.Trim(),
                DeviceName = request.DeviceName.Trim(),
                DeviceModel = request.DeviceModel.Trim(),
                OperatingSystem = request.OperatingSystem.Trim(),
                FirstSeenUtc = now,
                LastSeenUtc = now,
                Status = "Active"
            };

            _db.Installations.Add(installation);
        }
        else
        {
            installation.BuildVersion = request.BuildVersion.Trim();
            installation.Platform = request.Platform.Trim();
            installation.DeviceName = request.DeviceName.Trim();
            installation.DeviceModel = request.DeviceModel.Trim();
            installation.OperatingSystem = request.OperatingSystem.Trim();
            installation.LastSeenUtc = now;
            installation.Status = "Active";
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("installationUpdated", new
        {
            installationId = installation.InstallationId,
            buildVersion = installation.BuildVersion,
            lastSeenUtc = installation.LastSeenUtc
        }, cancellationToken);

        var response = new RegisterInstallationResponse
        {
            InstallationId = installation.InstallationId,
            GameCode = installation.GameCode,
            BuildVersion = installation.BuildVersion,
            FirstSeenUtc = installation.FirstSeenUtc,
            LastSeenUtc = installation.LastSeenUtc,
            ServerTimeUtc = now
        };

        return Ok(response);
    }
}