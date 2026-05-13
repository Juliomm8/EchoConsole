using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Contracts.Common;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/alerts")]
public sealed class AlertsAdminController : ControllerBase
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<AlertsAdminController> _logger;

    public AlertsAdminController(
        EchoConsoleDbContext dbContext,
        ILogger<AlertsAdminController> logger)
    {
        _dbContext = dbContext;
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
            if (!Enum.TryParse<AlertSeverity>(severity.Trim(), ignoreCase: true, out var severityValue))
            {
                return BadRequest(new
                {
                    message = $"Invalid severity value '{severity}'. Allowed values: {string.Join(", ", Enum.GetNames(typeof(AlertSeverity)))}"
                });
            }

            parsedSeverity = severityValue;
        }

        var query = _dbContext.SystemAlerts
            .AsNoTracking()
            .AsQueryable();

        if (parsedSeverity.HasValue)
        {
            query = query.Where(x => x.Severity == parsedSeverity.Value);
        }

        if (isResolved.HasValue)
        {
            query = query.Where(x => x.IsResolved == isResolved.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SystemAlertDto
            {
                Id = x.Id,
                Severity = x.Severity.ToString(),
                Message = x.Message,
                Source = x.Source,
                InstallationId = x.InstallationId,
                CreatedAtUtc = x.CreatedAtUtc,
                IsResolved = x.IsResolved,
                ResolvedAtUtc = x.ResolvedAtUtc
            })
            .ToListAsync(cancellationToken);

        var response = new PagedResponse<SystemAlertDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Ok(response);
    }

    [HttpPatch("{id:int}/resolve")]
    public async Task<ActionResult<SystemAlertDto>> Resolve(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SystemAlerts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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

            _logger.LogInformation(
                "System alert resolved. AlertId: {AlertId}, Severity: {Severity}, Source: {Source}",
                entity.Id,
                entity.Severity,
                entity.Source);
        }

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