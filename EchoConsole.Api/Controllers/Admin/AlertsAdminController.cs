using System.Data;
using System.Globalization;
using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Contracts.Common;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/alerts")]
public sealed class AlertsAdminController : ControllerBase
{
    private const string OpenStatus = "OPEN";
    private const string ResolvedStatus = "RESOLVED";
    private const string FallbackErrorTypeCode = "UNCLASSIFIED";

    private readonly EchoConsoleDbContext _dbContext;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<AlertsAdminController> _logger;

    public AlertsAdminController(
        EchoConsoleDbContext dbContext,
        IHubContext<TelemetryHub> hubContext,
        ILogger<AlertsAdminController> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<SystemAlertDto>>> GetAll(
        [FromQuery] string? severity,
        [FromQuery] string? status = OpenStatus,
        [FromQuery] bool? isResolved = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!TryParseSeverity(severity, out var parsedSeverity))
        {
            return BadRequest(new
            {
                message =
                    $"Invalid severity value '{severity}'. Allowed values: {string.Join(", ", Enum.GetNames<AlertSeverity>())}"
            });
        }

        if (!TryResolveStatus(status, isResolved, out var resolvedFilter))
        {
            return BadRequest(new
            {
                message =
                    $"Invalid status value '{status}'. Allowed values: {OpenStatus}, {ResolvedStatus}."
            });
        }

        var query = _dbContext.SystemAlerts
            .AsNoTracking()
            .Where(alert => alert.IsResolved == resolvedFilter);

        if (parsedSeverity.HasValue)
        {
            query = query.Where(
                alert => alert.Severity == parsedSeverity.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(alert => alert.CreatedAtUtc)
            .ThenByDescending(alert => alert.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(alert => new SystemAlertDto
            {
                Id = alert.Id,
                Severity = alert.Severity.ToString(),
                Status = alert.IsResolved
                    ? ResolvedStatus
                    : OpenStatus,
                ErrorTypeCode = alert.ErrorTypeCode,
                BuildVersion = alert.BuildVersion,
                Message = alert.Message,
                Source = alert.Source,
                InstallationId = alert.InstallationId,
                CreatedAtUtc = alert.CreatedAtUtc,
                IsResolved = alert.IsResolved,
                ResolvedAtUtc = alert.ResolvedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<SystemAlertDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        });
    }

    [HttpPatch("{id:int}/resolve")]
    public async Task<ActionResult<SystemAlertDto>> Resolve(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SystemAlerts
            .FirstOrDefaultAsync(
                alert => alert.Id == id,
                cancellationToken);

        if (entity is null)
        {
            return NotFound(new
            {
                message = $"Alert with id '{id}' was not found."
            });
        }

        if (!entity.IsResolved)
        {
            entity.IsResolved = true;
            entity.ResolvedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedAlert = MapAlert(entity);

            await _hubContext.Clients.All.SendAsync(
                "alertUpdated",
                updatedAlert,
                cancellationToken);

            _logger.LogInformation(
                "System alert resolved. AlertId={AlertId}, Severity={Severity}, ErrorType={ErrorTypeCode}, Source={Source}",
                entity.Id,
                entity.Severity,
                entity.ErrorTypeCode,
                entity.Source);
        }

        return Ok(MapAlert(entity));
    }

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<AlertTypeDefinitionDto>>>
        GetAlertTypes(
            CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.AlertTypeDefinitions
            .AsNoTracking()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Code)
            .Select(item => new AlertTypeDefinitionDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                DefaultSeverity = item.DefaultSeverity.ToString(),
                IsActive = item.IsActive,
                AlertCount = _dbContext.SystemAlerts.Count(
                    alert => alert.ErrorTypeCode == item.Code),
                UpdatedAtUtc = item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("types/{id:int}")]
    public async Task<ActionResult<AlertTypeDefinitionDto>> UpdateAlertType(
        int id,
        [FromBody] UpdateAlertTypeDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AlertSeverity>(
                request.DefaultSeverity,
                true,
                out var defaultSeverity))
        {
            return BadRequest(new
            {
                message =
                    $"Invalid default severity '{request.DefaultSeverity}'."
            });
        }

        var normalizedName = request.Name.Trim();
        var normalizedDescription = request.Description.Trim();

        if (normalizedName.Length == 0 ||
            normalizedDescription.Length == 0)
        {
            return BadRequest(new
            {
                message = "Name and description are required."
            });
        }

        var entity = await _dbContext.AlertTypeDefinitions
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = normalizedName;
        entity.Description = normalizedDescription;
        entity.DefaultSeverity = defaultSeverity;
        entity.IsActive = entity.Code == FallbackErrorTypeCode
            ? true
            : request.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await CreateAlertTypeDtoAsync(
            entity,
            cancellationToken));
    }

    [HttpDelete("types/{id:int}")]
    public async Task<IActionResult> DeleteAlertType(
        int id,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        var result = await executionStrategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

                try
                {
                    var entity = await _dbContext.AlertTypeDefinitions
                        .FirstOrDefaultAsync(
                            item => item.Id == id,
                            cancellationToken);

                    if (entity is null)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return AlertTypeDeleteResult.NotFound;
                    }

                    if (entity.Code == FallbackErrorTypeCode)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return AlertTypeDeleteResult.Protected;
                    }

                    var reassignedAlerts = await _dbContext.SystemAlerts
                        .Where(alert =>
                            alert.ErrorTypeCode == entity.Code)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(
                                alert => alert.ErrorTypeCode,
                                FallbackErrorTypeCode),
                            cancellationToken);

                    _dbContext.AlertTypeDefinitions.Remove(entity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return new AlertTypeDeleteResult(
                        true,
                        false,
                        false,
                        reassignedAlerts,
                        entity.Code);
                }
                catch
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw;
                }
            });

        if (result.WasNotFound)
        {
            return NotFound();
        }

        if (result.WasProtected)
        {
            return Conflict(new
            {
                message =
                    "The UNCLASSIFIED fallback category cannot be deleted."
            });
        }

        _logger.LogWarning(
            "Alert type deleted. Code={Code}, ReassignedAlerts={ReassignedAlerts}",
            result.Code,
            result.ReassignedAlerts);

        return NoContent();
    }

    [HttpPost("ai-trend-analysis")]
    public async Task<ActionResult<AlertAiTrendAnalysisDto>>
        RunAiTrendAnalysis(
            [FromQuery] string? culture,
            CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recentStart = now.AddHours(-24);
        var previousStart = now.AddHours(-48);

        var metrics = await _dbContext.SystemAlerts
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                PhysicalAlertCount = group.Count(),
                OpenAlertCount = group.Count(
                    alert => !alert.IsResolved),
                RecentAlertCount = group.Count(
                    alert => alert.CreatedAtUtc >= recentStart),
                RecentCriticalCount = group.Count(
                    alert =>
                        alert.CreatedAtUtc >= recentStart &&
                        (alert.Severity == AlertSeverity.Critical ||
                         alert.Severity == AlertSeverity.Fatal)),
                PreviousCriticalCount = group.Count(
                    alert =>
                        alert.CreatedAtUtc >= previousStart &&
                        alert.CreatedAtUtc < recentStart &&
                        (alert.Severity == AlertSeverity.Critical ||
                         alert.Severity == AlertSeverity.Fatal))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var physicalAlertCount = metrics?.PhysicalAlertCount ?? 0;
        var openAlertCount = metrics?.OpenAlertCount ?? 0;
        var recentAlertCount = metrics?.RecentAlertCount ?? 0;
        var recentCriticalCount = metrics?.RecentCriticalCount ?? 0;
        var previousCriticalCount = metrics?.PreviousCriticalCount ?? 0;

        var activeBuildVersion = await _dbContext.GameBuilds
            .AsNoTracking()
            .Where(build => build.IsActive)
            .OrderByDescending(build => build.ReleaseDateUtc)
            .Select(build => build.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "NO_ACTIVE_BUILD";

        var isSpanish = culture?.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase) == true;

        if (openAlertCount == 0)
        {
            var nominalNarrative = isSpanish
                ? "ESTACIÓN NOC INICIALIZADA: Pipeline de telemetría libre de trazas de error. Todo el hardware del juego opera bajo parámetros nominales óptimos."
                : "NOC STATION INITIALIZED: The telemetry pipeline is free of error traces. All game hardware is operating within optimal nominal parameters.";

            return Ok(new AlertAiTrendAnalysisDto
            {
                Narrative = nominalNarrative,
                ActiveBuildVersion = activeBuildVersion,
                DominantSource = physicalAlertCount == 0
                    ? "NOC_TELEMETRY_CORE"
                    : "RESOLVED_ARCHIVE",
                RecentAlertCount = recentAlertCount,
                OpenAlertCount = 0,
                RecentCriticalCount = recentCriticalCount,
                PreviousCriticalCount = previousCriticalCount,
                CriticalTrendPercent = 0m,
                GeneratedAtUtc = now
            });
        }

        var dominantSource = await _dbContext.SystemAlerts
            .AsNoTracking()
            .Where(alert => !alert.IsResolved)
            .GroupBy(alert => alert.Source)
            .Select(group => new
            {
                Source = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Source)
            .Select(item => item.Source)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "NOC_TELEMETRY_CORE";

        var criticalTrendPercent = previousCriticalCount == 0
            ? recentCriticalCount > 0
                ? 100m
                : 0m
            : Math.Round(
                (recentCriticalCount - previousCriticalCount) * 100m /
                previousCriticalCount,
                1,
                MidpointRounding.AwayFromZero);

        var narrative = isSpanish
            ? BuildSpanishNarrative(
                activeBuildVersion,
                dominantSource,
                recentAlertCount,
                openAlertCount,
                recentCriticalCount,
                criticalTrendPercent)
            : BuildEnglishNarrative(
                activeBuildVersion,
                dominantSource,
                recentAlertCount,
                openAlertCount,
                recentCriticalCount,
                criticalTrendPercent);

        return Ok(new AlertAiTrendAnalysisDto
        {
            Narrative = narrative,
            ActiveBuildVersion = activeBuildVersion,
            DominantSource = dominantSource,
            RecentAlertCount = recentAlertCount,
            OpenAlertCount = openAlertCount,
            RecentCriticalCount = recentCriticalCount,
            PreviousCriticalCount = previousCriticalCount,
            CriticalTrendPercent = criticalTrendPercent,
            GeneratedAtUtc = now
        });
    }

    private async Task<AlertTypeDefinitionDto> CreateAlertTypeDtoAsync(
        AlertTypeDefinition entity,
        CancellationToken cancellationToken)
    {
        var alertCount = await _dbContext.SystemAlerts
            .AsNoTracking()
            .CountAsync(
                alert => alert.ErrorTypeCode == entity.Code,
                cancellationToken);

        return new AlertTypeDefinitionDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            DefaultSeverity = entity.DefaultSeverity.ToString(),
            IsActive = entity.IsActive,
            AlertCount = alertCount,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static bool TryParseSeverity(
        string? severity,
        out AlertSeverity? parsedSeverity)
    {
        parsedSeverity = null;

        if (string.IsNullOrWhiteSpace(severity))
        {
            return true;
        }

        if (!Enum.TryParse<AlertSeverity>(
                severity.Trim(),
                true,
                out var value))
        {
            return false;
        }

        parsedSeverity = value;
        return true;
    }

    private static bool TryResolveStatus(
        string? status,
        bool? isResolved,
        out bool resolved)
    {
        if (isResolved.HasValue)
        {
            resolved = isResolved.Value;
            return true;
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? OpenStatus
            : status.Trim().ToUpperInvariant();

        if (normalizedStatus == OpenStatus)
        {
            resolved = false;
            return true;
        }

        if (normalizedStatus == ResolvedStatus)
        {
            resolved = true;
            return true;
        }

        resolved = false;
        return false;
    }

    private static SystemAlertDto MapAlert(SystemAlert entity)
    {
        return new SystemAlertDto
        {
            Id = entity.Id,
            Severity = entity.Severity.ToString(),
            Status = entity.IsResolved
                ? ResolvedStatus
                : OpenStatus,
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

    private static string BuildSpanishNarrative(
        string activeBuildVersion,
        string dominantSource,
        int recentAlertCount,
        int openAlertCount,
        int recentCriticalCount,
        decimal criticalTrendPercent)
    {
        return
            $"Análisis de Tendencias IA: la build activa {activeBuildVersion} registró {recentAlertCount} alertas durante las últimas 24 horas, con {openAlertCount} incidencias aún abiertas y {recentCriticalCount} anomalías críticas o fatales. La variación crítica es de {criticalTrendPercent.ToString("0.0", CultureInfo.InvariantCulture)}% respecto al periodo anterior. El origen dominante es {dominantSource}. El patrón estocástico sugiere revisar congestión del hilo principal, presión sobre el subsistema de telemetría local y ráfagas de procesamiento simultáneo antes del siguiente despliegue de Cosmic Diner.";
    }

    private static string BuildEnglishNarrative(
        string activeBuildVersion,
        string dominantSource,
        int recentAlertCount,
        int openAlertCount,
        int recentCriticalCount,
        decimal criticalTrendPercent)
    {
        return
            $"AI Trend Analysis: active build {activeBuildVersion} produced {recentAlertCount} alerts during the last 24 hours, with {openAlertCount} incidents still open and {recentCriticalCount} critical or fatal anomalies. Critical volume changed by {criticalTrendPercent.ToString("0.0", CultureInfo.InvariantCulture)}% compared with the previous period. The dominant source is {dominantSource}. The stochastic pattern suggests investigating main-thread congestion, pressure in the local telemetry subsystem, and simultaneous workload bursts before the next Cosmic Diner deployment.";
    }

    private sealed record AlertTypeDeleteResult(
        bool Deleted,
        bool WasNotFound,
        bool WasProtected,
        int ReassignedAlerts,
        string Code)
    {
        public static AlertTypeDeleteResult NotFound { get; } =
            new(false, true, false, 0, string.Empty);

        public static AlertTypeDeleteResult Protected { get; } =
            new(false, false, true, 0, FallbackErrorTypeCode);
    }
}
