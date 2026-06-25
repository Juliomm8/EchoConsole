using System.Data;
using System.Globalization;
using System.Threading;
using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/installations")]
public sealed class InstallationsAdminController : ControllerBase
{
    private static readonly HashSet<string> AllowedAdminStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "Inactive",
            "Banned"
        };

    private static int _adminMetadataSchemaReady;

    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<InstallationsAdminController> _logger;

    public InstallationsAdminController(
        EchoConsoleDbContext dbContext,
        ILogger<InstallationsAdminController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedInstallationsResponse>> GetAll(
        [FromQuery] GetInstallationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await AdminMetadataSchemaIsReadyAsync(cancellationToken))
        {
            return AdminMetadataSchemaNotReady();
        }

        var query = _dbContext.Installations
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var likePattern = $"%{search}%";

            query = Guid.TryParse(search, out var installationId)
                ? query.Where(installation =>
                    installation.InstallationId == installationId ||
                    EF.Functions.Like(
                        installation.DeviceName,
                        likePattern) ||
                    (installation.AdminAlias != null &&
                     EF.Functions.Like(
                         installation.AdminAlias,
                         likePattern)) ||
                    EF.Functions.Like(
                        installation.BuildVersion,
                        likePattern) ||
                    (installation.OwnerUser != null &&
                     EF.Functions.Like(
                         installation.OwnerUser.Alias,
                         likePattern)))
                : query.Where(installation =>
                    EF.Functions.Like(
                        installation.DeviceName,
                        likePattern) ||
                    (installation.AdminAlias != null &&
                     EF.Functions.Like(
                         installation.AdminAlias,
                         likePattern)) ||
                    EF.Functions.Like(
                        installation.BuildVersion,
                        likePattern) ||
                    (installation.OwnerUser != null &&
                     EF.Functions.Like(
                         installation.OwnerUser.Alias,
                         likePattern)));
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var items = await ProjectInstallations(query)
            .OrderByDescending(item => item.LastUpdateUtc)
            .ThenBy(item => item.AdminAlias)
            .ThenBy(item => item.DeviceName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedInstallationsResponse
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)request.PageSize),
            Items = items
        });
    }

    [HttpGet("{installationId:guid}")]
    public async Task<ActionResult<InstallationListItemDto>> GetById(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        if (!await AdminMetadataSchemaIsReadyAsync(cancellationToken))
        {
            return AdminMetadataSchemaNotReady();
        }

        var installation = await ProjectInstallations(
                _dbContext.Installations
                    .AsNoTracking()
                    .Where(item =>
                        item.InstallationId == installationId))
            .FirstOrDefaultAsync(cancellationToken);

        return installation is null
            ? NotFound()
            : Ok(installation);
    }

    [HttpPatch("{installationId:guid}/metadata")]
    public async Task<ActionResult<InstallationListItemDto>> UpdateMetadata(
        Guid installationId,
        [FromBody] UpdateInstallationAdminMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (!await AdminMetadataSchemaIsReadyAsync(cancellationToken))
        {
            return AdminMetadataSchemaNotReady();
        }

        var requestedStatus = request.AdminStatus.Trim();

        if (!AllowedAdminStatuses.Contains(requestedStatus))
        {
            ModelState.AddModelError(
                nameof(request.AdminStatus),
                "AdminStatus must be Active, Inactive, or Banned.");

            return ValidationProblem(ModelState);
        }

        var normalizedStatus = requestedStatus.Equals(
            "Inactive",
            StringComparison.OrdinalIgnoreCase)
            ? "Inactive"
            : requestedStatus.Equals(
                "Banned",
                StringComparison.OrdinalIgnoreCase)
                ? "Banned"
                : "Active";

        var normalizedAlias = string.IsNullOrWhiteSpace(
            request.AdminAlias)
            ? null
            : request.AdminAlias.Trim();

        var updatedRows = await _dbContext.Installations
            .Where(item => item.InstallationId == installationId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.AdminAlias,
                        normalizedAlias)
                    .SetProperty(
                        item => item.AdminStatus,
                        normalizedStatus),
                cancellationToken);

        if (updatedRows == 0)
        {
            return NotFound();
        }

        var updatedInstallation = await ProjectInstallations(
                _dbContext.Installations
                    .AsNoTracking()
                    .Where(item =>
                        item.InstallationId == installationId))
            .SingleAsync(cancellationToken);

        _logger.LogInformation(
            "Installation metadata updated. InstallationId={InstallationId}, AdminAlias={AdminAlias}, AdminStatus={AdminStatus}",
            installationId,
            normalizedAlias,
            normalizedStatus);

        return Ok(updatedInstallation);
    }

    [HttpDelete("{installationId:guid}")]
    public async Task<IActionResult> Delete(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        var deleted = await executionStrategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        cancellationToken);

                try
                {
                    var installation = await _dbContext.Installations
                        .AsNoTracking()
                        .Where(item =>
                            item.InstallationId == installationId)
                        .Select(item => new
                        {
                            item.Id,
                            item.DeviceName
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (installation is null)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return false;
                    }

                    var sessionIds = _dbContext.GameSessions
                        .Where(session =>
                            session.InstallationDbId == installation.Id)
                        .Select(session => session.Id);

                    await _dbContext.GameSessionEvents
                        .Where(sessionEvent =>
                            sessionIds.Contains(
                                sessionEvent.GameSessionId))
                        .ExecuteDeleteAsync(cancellationToken);

                    await _dbContext.GameSessions
                        .Where(session =>
                            session.InstallationDbId == installation.Id)
                        .ExecuteDeleteAsync(cancellationToken);

                    await _dbContext.SystemAlerts
                        .Where(alert =>
                            alert.InstallationId ==
                                installation.DeviceName ||
                            alert.InstallationId ==
                                installationId.ToString())
                        .ExecuteDeleteAsync(cancellationToken);

                    var deletedRows = await _dbContext.Installations
                        .Where(item => item.Id == installation.Id)
                        .ExecuteDeleteAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);

                    return deletedRows > 0;
                }
                catch
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw;
                }
            });

        if (!deleted)
        {
            return NotFound();
        }

        _logger.LogWarning(
            "Installation and related telemetry deleted. InstallationId={InstallationId}",
            installationId);

        return NoContent();
    }

    private async Task<bool> AdminMetadataSchemaIsReadyAsync(
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _adminMetadataSchemaReady) == 1)
        {
            return true;
        }

        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText =
                """
                SELECT CASE
                    WHEN COL_LENGTH(N'dbo.Installations', N'AdminAlias') IS NOT NULL
                     AND COL_LENGTH(N'dbo.Installations', N'AdminStatus') IS NOT NULL
                    THEN 1
                    ELSE 0
                END;
                """;

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            var schemaIsReady = Convert.ToInt32(
                result,
                CultureInfo.InvariantCulture) == 1;

            if (schemaIsReady)
            {
                Interlocked.Exchange(
                    ref _adminMetadataSchemaReady,
                    1);
            }

            return schemaIsReady;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private ObjectResult AdminMetadataSchemaNotReady()
    {
        return Problem(
            statusCode:
                StatusCodes.Status503ServiceUnavailable,
            title:
                "Installation metadata schema is not ready.",
            detail:
                "Apply migration AddInstallationAdminMetadata before using administrative installation endpoints.");
    }

    private static IQueryable<InstallationListItemDto>
        ProjectInstallations(
            IQueryable<Installation> query)
    {
        return query.Select(item => new InstallationListItemDto
        {
            DatabaseId = item.Id,
            InstallationId = item.InstallationId,
            GameCode = item.GameCode,
            BuildVersion = item.BuildVersion,
            Platform = item.Platform,
            DeviceName = item.DeviceName,
            DeviceModel = item.DeviceModel,
            OSVersion = item.OSVersion,
            Processor = item.Processor,
            Gpu = item.Gpu,
            RamMb = item.RamMb,
            TelemetryStatus = item.Status,
            AdminAlias = item.AdminAlias,
            AdminStatus = item.AdminStatus,
            OwnerUserId = item.OwnerUserId,
            OwnerAlias = item.OwnerUser != null
                ? item.OwnerUser.Alias
                : null,
            FirstSeenUtc = item.FirstSeenUtc,
            LastUpdateUtc = item.LastUpdateUtc
        });
    }
}
