using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Contracts.Client;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Client;

[ApiController]
[Route("api/client/alerts")]
[EnableRateLimiting("client-ingest")]
public sealed class AlertsController : ControllerBase
{
    private const string ExpectedGameCode = "cosmic-diner";
    private const string FallbackErrorTypeCode = "UNCLASSIFIED";

    private readonly EchoConsoleDbContext _dbContext;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        EchoConsoleDbContext dbContext,
        IHubContext<TelemetryHub> hubContext,
        ILogger<AlertsController> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SystemAlertDto>> Create(
        [FromBody] CreateSystemAlertRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                request.GameCode,
                ExpectedGameCode,
                StringComparison.Ordinal))
        {
            return BadRequest("Invalid gameCode.");
        }

        var errorTypeCode = NormalizeErrorTypeCode(
            request.ErrorTypeCode);

        var alertType = await _dbContext.AlertTypeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Code == errorTypeCode && item.IsActive,
                cancellationToken);

        if (alertType is null)
        {
            errorTypeCode = FallbackErrorTypeCode;

            alertType = await _dbContext.AlertTypeDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Code == FallbackErrorTypeCode,
                    cancellationToken);
        }

        if (!TryResolveSeverity(
                request.Severity,
                alertType?.DefaultSeverity ?? AlertSeverity.Warning,
                out var severity))
        {
            return BadRequest(new
            {
                message =
                    $"Invalid severity '{request.Severity}'. Allowed values: {string.Join(", ", Enum.GetNames<AlertSeverity>())}"
            });
        }

        var installationId = string.IsNullOrWhiteSpace(
            request.InstallationId)
                ? null
                : request.InstallationId.Trim();

        var buildVersion = await ResolveBuildVersionAsync(
            request.BuildVersion,
            installationId,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var entity = new SystemAlert
        {
            Severity = severity,
            ErrorTypeCode = errorTypeCode,
            BuildVersion = buildVersion,
            Message = request.Message.Trim(),
            Source = request.Source.Trim(),
            InstallationId = installationId,
            CreatedAtUtc = now,
            IsResolved = false,
            ResolvedAtUtc = null
        };

        _dbContext.SystemAlerts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Client alert created. AlertId={AlertId}, Severity={Severity}, ErrorType={ErrorTypeCode}, Source={Source}, InstallationId={InstallationId}",
            entity.Id,
            entity.Severity,
            entity.ErrorTypeCode,
            entity.Source,
            entity.InstallationId);

        var dto = MapAlert(entity);

        await _hubContext.Clients.All.SendAsync(
            "alertCreated",
            dto,
            cancellationToken);

        return Ok(dto);
    }

    private async Task<string?> ResolveBuildVersionAsync(
        string? requestedBuildVersion,
        string? installationId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedBuildVersion))
        {
            return requestedBuildVersion.Trim();
        }

        if (string.IsNullOrWhiteSpace(installationId))
        {
            return await GetActiveBuildVersionAsync(cancellationToken);
        }

        var normalizedInstallationId = installationId.Trim();
        var hasGuid = Guid.TryParse(
            normalizedInstallationId,
            out var installationGuid);

        var installationBuild = await _dbContext.Installations
            .AsNoTracking()
            .Where(installation =>
                installation.DeviceName == normalizedInstallationId ||
                (hasGuid && installation.InstallationId == installationGuid))
            .OrderByDescending(installation => installation.LastUpdateUtc)
            .Select(installation => installation.BuildVersion)
            .FirstOrDefaultAsync(cancellationToken);

        return installationBuild
            ?? await GetActiveBuildVersionAsync(cancellationToken);
    }

    private async Task<string?> GetActiveBuildVersionAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.GameBuilds
            .AsNoTracking()
            .Where(build => build.IsActive)
            .OrderByDescending(build => build.ReleaseDateUtc)
            .Select(build => build.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool TryResolveSeverity(
        string? requestedSeverity,
        AlertSeverity fallbackSeverity,
        out AlertSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(requestedSeverity))
        {
            severity = fallbackSeverity;
            return true;
        }

        return Enum.TryParse(
            requestedSeverity.Trim(),
            true,
            out severity);
    }

    private static string NormalizeErrorTypeCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? FallbackErrorTypeCode
            : value.Trim().ToUpperInvariant();
    }

    private static SystemAlertDto MapAlert(SystemAlert entity)
    {
        return new SystemAlertDto
        {
            Id = entity.Id,
            Severity = entity.Severity.ToString(),
            Status = entity.IsResolved ? "RESOLVED" : "OPEN",
            ErrorTypeCode = entity.ErrorTypeCode,
            BuildVersion = entity.BuildVersion,
            Message = entity.Message,
            Source = entity.Source,
            InstallationId = entity.InstallationId,
            CreatedAtUtc = entity.CreatedAtUtc,
            IsResolved = entity.IsResolved,
            ResolvedAtUtc = entity.ResolvedAtUtc
        };
    }
}
