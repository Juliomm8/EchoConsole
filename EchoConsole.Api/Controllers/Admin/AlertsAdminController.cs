using System.Globalization;
using System.Net.Http.Json;
using System.Text;
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
    private const string DiscordWebhookUrl =
        "https://discord.com/api/webhooks/1519668721822601342/BNA_JxgHEPpkfyDuHY08Ru9soqYrBPXs4ZafenKh2Zr_mQc9Nk7jzrTZokUq2ofVecSi";

    private readonly EchoConsoleDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<AlertsAdminController> _logger;

    public AlertsAdminController(
        EchoConsoleDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IHubContext<TelemetryHub> hubContext,
        ILogger<AlertsAdminController> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<SystemAlertDto>>> GetAll(
        [FromQuery] string? severity,
        [FromQuery] bool? isResolved,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        AlertSeverity? parsedSeverity = null;

        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (!Enum.TryParse<AlertSeverity>(
                    severity.Trim(),
                    true,
                    out var severityValue))
            {
                return BadRequest(new
                {
                    message =
                        $"Invalid severity value '{severity}'. Allowed values: {string.Join(", ", Enum.GetNames<AlertSeverity>())}"
                });
            }

            parsedSeverity = severityValue;
        }

        var query = _dbContext.SystemAlerts
            .AsNoTracking()
            .AsQueryable();

        if (parsedSeverity.HasValue)
        {
            query = query.Where(
                alert => alert.Severity == parsedSeverity.Value);
        }

        if (isResolved.HasValue)
        {
            query = query.Where(
                alert => alert.IsResolved == isResolved.Value);
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

            await _hubContext.Clients.All.SendAsync(
                "alertUpdated",
                new
                {
                    alertId = entity.Id,
                    isResolved = entity.IsResolved,
                    resolvedAtUtc = entity.ResolvedAtUtc
                },
                cancellationToken);

            _logger.LogInformation(
                "System alert resolved. AlertId={AlertId}, Severity={Severity}, Source={Source}",
                entity.Id,
                entity.Severity,
                entity.Source);
        }

        return Ok(MapAlert(entity));
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

        var recentQuery = _dbContext.SystemAlerts
            .AsNoTracking()
            .Where(alert => alert.CreatedAtUtc >= recentStart);

        var recentAlertCount = await recentQuery.CountAsync(
            cancellationToken);

        var openAlertCount = await recentQuery.CountAsync(
            alert => !alert.IsResolved,
            cancellationToken);

        var recentCriticalCount = await recentQuery.CountAsync(
            alert =>
                alert.Severity == AlertSeverity.Critical ||
                alert.Severity == AlertSeverity.Fatal,
            cancellationToken);

        var previousCriticalCount = await _dbContext.SystemAlerts
            .AsNoTracking()
            .CountAsync(
                alert =>
                    alert.CreatedAtUtc >= previousStart &&
                    alert.CreatedAtUtc < recentStart &&
                    (alert.Severity == AlertSeverity.Critical ||
                     alert.Severity == AlertSeverity.Fatal),
                cancellationToken);

        var dominantSource = await recentQuery
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
            ?? "TelemetryCore";

        var activeBuildVersion = await _dbContext.GameBuilds
            .AsNoTracking()
            .Where(build => build.IsActive)
            .OrderByDescending(build => build.ReleaseDateUtc)
            .Select(build => build.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "v2.0.0-RetroUI";

        var criticalTrendPercent = previousCriticalCount == 0
            ? recentCriticalCount > 0 ? 100m : 0m
            : Math.Round(
                (recentCriticalCount - previousCriticalCount) * 100m /
                previousCriticalCount,
                1,
                MidpointRounding.AwayFromZero);

        var isSpanish = culture?.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase) == true;

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

    [HttpPost("broadcast-discord")]
    public async Task<ActionResult<AlertDiscordBroadcastDto>>
        BroadcastDiscord(
            CancellationToken cancellationToken = default)
    {
        if (DiscordWebhookUrl.Contains(
                "REPLACE_WITH_REAL_WEBHOOK_URL",
                StringComparison.Ordinal))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new AlertDiscordBroadcastDto
                {
                    Sent = false,
                    Message =
                        "Discord webhook is not configured. Replace DiscordWebhookUrl in AlertsAdminController.",
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });
        }

        var alerts = await _dbContext.SystemAlerts
            .AsNoTracking()
            .Where(alert =>
                !alert.IsResolved &&
                (alert.Severity == AlertSeverity.Critical ||
                 alert.Severity == AlertSeverity.Fatal))
            .OrderByDescending(alert => alert.CreatedAtUtc)
            .Take(10)
            .Select(alert => new
            {
                alert.Id,
                Severity = alert.Severity.ToString(),
                alert.Source,
                alert.Message,
                alert.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (alerts.Count == 0)
        {
            return Ok(new AlertDiscordBroadcastDto
            {
                Sent = false,
                AlertCount = 0,
                Message = "No unresolved critical alerts are available for broadcast.",
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var builder = new StringBuilder();
        builder.AppendLine("**Echo Console Critical Alert Broadcast**");
        builder.AppendLine("Cosmic Diner telemetry incident summary:");

        foreach (var alert in alerts)
        {
            builder.Append("• #")
                .Append(alert.Id)
                .Append(" [")
                .Append(alert.Severity)
                .Append("] ")
                .Append(alert.Source)
                .Append(" — ")
                .AppendLine(alert.Message);
        }

        var content = builder.ToString();

        if (content.Length > 1900)
        {
            content = content[..1900] + "\n[TRUNCATED]";
        }

        var httpClient = _httpClientFactory.CreateClient();

        using var response = await httpClient.PostAsJsonAsync(
            DiscordWebhookUrl,
            new { content },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            _logger.LogWarning(
                "Discord broadcast failed. StatusCode={StatusCode}, Body={Body}",
                response.StatusCode,
                responseBody);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new AlertDiscordBroadcastDto
                {
                    Sent = false,
                    AlertCount = alerts.Count,
                    Message = "Discord rejected the webhook request.",
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });
        }

        _logger.LogInformation(
            "Discord alert broadcast completed. AlertCount={AlertCount}",
            alerts.Count);

        return Ok(new AlertDiscordBroadcastDto
        {
            Sent = true,
            AlertCount = alerts.Count,
            Message = "Critical alert broadcast completed successfully.",
            ProcessedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static SystemAlertDto MapAlert(
        SystemAlert entity)
    {
        return new SystemAlertDto
        {
            Id = entity.Id,
            Severity = entity.Severity.ToString(),
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
}
