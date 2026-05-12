using EchoConsole.Api.Contracts.Client;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
                OSVersion = request.OperatingSystem.Trim(),
                Processor = string.IsNullOrWhiteSpace(request.Processor) ? null : request.Processor.Trim(),
                Gpu = string.IsNullOrWhiteSpace(request.Gpu) ? null : request.Gpu.Trim(),
                RamMb = request.RamMb,
                FirstSeenUtc = now,
                LastUpdateUtc = now,
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
            installation.OSVersion = request.OperatingSystem.Trim();
            installation.Processor = string.IsNullOrWhiteSpace(request.Processor) ? installation.Processor : request.Processor.Trim();
            installation.Gpu = string.IsNullOrWhiteSpace(request.Gpu) ? installation.Gpu : request.Gpu.Trim();
            installation.RamMb = request.RamMb ?? installation.RamMb;
            installation.LastUpdateUtc = now;
            installation.Status = "Active";
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.All.SendAsync("installationUpdated", new
        {
            installationId = installation.InstallationId,
            deviceName = installation.DeviceName,
            buildVersion = installation.BuildVersion,
            lastUpdateUtc = installation.LastUpdateUtc
        }, cancellationToken);

        var response = new RegisterInstallationResponse
        {
            InstallationId = installation.InstallationId,
            GameCode = installation.GameCode,
            BuildVersion = installation.BuildVersion,
            FirstSeenUtc = installation.FirstSeenUtc,
            LastSeenUtc = installation.LastUpdateUtc,
            ServerTimeUtc = now
        };

        return Ok(response);
    }
}