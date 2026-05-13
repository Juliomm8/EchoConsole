using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Contracts.Client;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace EchoConsole.Api.Controllers.Client;

[ApiController]
[Route("api/client/alerts")]
[EnableRateLimiting("client-ingest")]
public sealed class AlertsController : ControllerBase
{
    private const string ExpectedGameCode = "cosmic-diner";

    private readonly EchoConsoleDbContext _dbContext;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        EchoConsoleDbContext dbContext,
        IHubContext<TelemetryHub> hub,
        ILogger<AlertsController> logger)
    {
        _dbContext = dbContext;
        _hub = hub;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SystemAlertDto>> Create(
        [FromBody] CreateSystemAlertRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.GameCode, ExpectedGameCode, StringComparison.Ordinal))
        {
            return BadRequest("Invalid gameCode.");
        }

        if (!Enum.TryParse<AlertSeverity>(request.Severity.Trim(), ignoreCase: true, out var severity))
        {
            return BadRequest(new
            {
                message = $"Invalid severity '{request.Severity}'. Allowed values: {string.Join(", ", Enum.GetNames(typeof(AlertSeverity)))}"
            });
        }

        var now = DateTimeOffset.UtcNow;

        var entity = new SystemAlert
        {
            Severity = severity,
            Message = request.Message.Trim(),
            Source = request.Source.Trim(),
            InstallationId = string.IsNullOrWhiteSpace(request.InstallationId)
                ? null
                : request.InstallationId.Trim(),
            CreatedAtUtc = now,
            IsResolved = false,
            ResolvedAtUtc = null
        };

        _dbContext.SystemAlerts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Client alert created. AlertId: {AlertId}, Severity: {Severity}, Source: {Source}, InstallationId: {InstallationId}",
            entity.Id,
            entity.Severity,
            entity.Source,
            entity.InstallationId);

        await _hub.Clients.All.SendAsync("alertCreated", new
        {
            id = entity.Id,
            severity = entity.Severity.ToString(),
            message = entity.Message,
            source = entity.Source,
            installationId = entity.InstallationId,
            createdAtUtc = entity.CreatedAtUtc,
            isResolved = entity.IsResolved
        }, cancellationToken);

        var dto = new SystemAlertDto
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

        return Ok(dto);
    }
}