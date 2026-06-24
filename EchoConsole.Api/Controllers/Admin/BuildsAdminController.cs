using System.Data;
using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Contracts.Common;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/builds")]
public sealed class BuildsAdminController : ControllerBase
{
    private const string ActiveBuildDeleteCode = "active_build_cannot_be_deleted";
    private const string ReferencedBuildDeleteCode = "build_has_linked_telemetry";
    private const string DuplicateVersionCode = "duplicate_build_version";

    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<BuildsAdminController> _logger;

    public BuildsAdminController(
        EchoConsoleDbContext dbContext,
        ILogger<BuildsAdminController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<GameBuildDto>>> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var query = _dbContext.GameBuilds
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim();

            query = query.Where(x =>
                x.VersionNumber.Contains(search) ||
                x.EngineVersion.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.ReleaseDateUtc)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new GameBuildDto
            {
                Id = x.Id,
                VersionNumber = x.VersionNumber,
                ReleaseNotes = x.ReleaseNotes,
                ReleaseDateUtc = x.ReleaseDateUtc,
                IsActive = x.IsActive,
                EngineVersion = x.EngineVersion
            })
            .ToListAsync(cancellationToken);

        var versions = items
            .Select(x => x.VersionNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (versions.Length > 0)
        {
            var installationCounts = await _dbContext.Installations
                .AsNoTracking()
                .Where(x => versions.Contains(x.BuildVersion))
                .GroupBy(x => x.BuildVersion)
                .Select(group => new
                {
                    BuildVersion = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(
                    x => x.BuildVersion,
                    x => x.Count,
                    cancellationToken);

            var sessionCounts = await _dbContext.GameSessions
                .AsNoTracking()
                .Where(x => versions.Contains(x.BuildVersion))
                .GroupBy(x => x.BuildVersion)
                .Select(group => new
                {
                    BuildVersion = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(
                    x => x.BuildVersion,
                    x => x.Count,
                    cancellationToken);

            foreach (var item in items)
            {
                item.LinkedInstallations = installationCounts.GetValueOrDefault(item.VersionNumber);
                item.TotalSessions = sessionCounts.GetValueOrDefault(item.VersionNumber);
            }
        }

        return Ok(new PagedResponse<GameBuildDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<GameBuildSummaryDto>> GetSummary(
        CancellationToken cancellationToken = default)
    {
        var totalBuilds = await _dbContext.GameBuilds
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeBuild = await _dbContext.GameBuilds
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.ReleaseDateUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.VersionNumber,
                x.EngineVersion
            })
            .FirstOrDefaultAsync(cancellationToken);

        var fallbackBuild = activeBuild is null
            ? await _dbContext.GameBuilds
                .AsNoTracking()
                .OrderByDescending(x => x.ReleaseDateUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => new
                {
                    x.VersionNumber,
                    x.EngineVersion
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return Ok(new GameBuildSummaryDto
        {
            TotalBuilds = totalBuilds,
            ActiveVersion = activeBuild?.VersionNumber ?? string.Empty,
            BaseEngineVersion = activeBuild?.EngineVersion
                ?? fallbackBuild?.EngineVersion
                ?? string.Empty
        });
    }

    [HttpPost]
    public async Task<ActionResult<GameBuildDto>> Create(
        [FromBody] CreateGameBuildRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedVersion = request.VersionNumber.Trim();
        var normalizedEngineVersion = request.EngineVersion.Trim();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await _dbContext.GameBuilds
            .AnyAsync(x => x.VersionNumber == normalizedVersion, cancellationToken);

        if (existing)
        {
            return Conflict(CreateError(
                DuplicateVersionCode,
                $"A build with version '{normalizedVersion}' already exists."));
        }

        if (request.IsActive)
        {
            await DeactivateOtherBuildsAsync(null, cancellationToken);
        }

        var entity = new GameBuild
        {
            VersionNumber = normalizedVersion,
            ReleaseNotes = NormalizeReleaseNotes(request.ReleaseNotes),
            ReleaseDateUtc = request.ReleaseDateUtc,
            IsActive = request.IsActive,
            EngineVersion = normalizedEngineVersion
        };

        _dbContext.GameBuilds.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "New game build created. Version: {VersionNumber}, EngineVersion: {EngineVersion}, IsActive: {IsActive}",
            entity.VersionNumber,
            entity.EngineVersion,
            entity.IsActive);

        return CreatedAtAction(
            nameof(GetAll),
            new { searchTerm = entity.VersionNumber, pageNumber = 1, pageSize = 20 },
            await CreateDtoAsync(entity, cancellationToken));
    }

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<GameBuildDto>> SetActive(
        int id,
        [FromBody] SetGameBuildActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var build = await _dbContext.GameBuilds
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (build is null)
        {
            return NotFound();
        }

        if (request.IsActive)
        {
            await DeactivateOtherBuildsAsync(id, cancellationToken);
        }

        build.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game build active status updated. BuildId: {BuildId}, Version: {VersionNumber}, IsActive: {IsActive}",
            build.Id,
            build.VersionNumber,
            build.IsActive);

        return Ok(await CreateDtoAsync(build, cancellationToken));
    }

    [HttpPatch("{id:int}/toggle-active")]
    public async Task<ActionResult<GameBuildDto>> ToggleActive(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var build = await _dbContext.GameBuilds
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (build is null)
        {
            return NotFound();
        }

        var shouldActivate = !build.IsActive;

        if (shouldActivate)
        {
            await DeactivateOtherBuildsAsync(id, cancellationToken);
        }
        else
        {
            await DeactivateOtherBuildsAsync(null, cancellationToken);
        }

        build.IsActive = shouldActivate;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game build toggled. BuildId: {BuildId}, Version: {VersionNumber}, IsActive: {IsActive}",
            build.Id,
            build.VersionNumber,
            build.IsActive);

        return Ok(await CreateDtoAsync(build, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GameBuildDto>> Update(
        int id,
        [FromBody] UpdateGameBuildRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedVersion = request.VersionNumber.Trim();
        var normalizedEngineVersion = request.EngineVersion.Trim();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var build = await _dbContext.GameBuilds
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (build is null)
        {
            return NotFound();
        }

        var duplicateVersion = await _dbContext.GameBuilds
            .AnyAsync(
                x => x.Id != id && x.VersionNumber == normalizedVersion,
                cancellationToken);

        if (duplicateVersion)
        {
            return Conflict(CreateError(
                DuplicateVersionCode,
                $"A build with version '{normalizedVersion}' already exists."));
        }

        var previousVersion = build.VersionNumber;

        if (!string.Equals(
                previousVersion,
                normalizedVersion,
                StringComparison.Ordinal))
        {
            await _dbContext.Installations
                .Where(x => x.BuildVersion == previousVersion)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.BuildVersion,
                        normalizedVersion),
                    cancellationToken);

            await _dbContext.GameSessions
                .Where(x => x.BuildVersion == previousVersion)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.BuildVersion,
                        normalizedVersion),
                    cancellationToken);
        }

        build.VersionNumber = normalizedVersion;
        build.EngineVersion = normalizedEngineVersion;
        build.ReleaseDateUtc = request.ReleaseDateUtc;
        build.ReleaseNotes = NormalizeReleaseNotes(request.ReleaseNotes);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game build updated. BuildId: {BuildId}, PreviousVersion: {PreviousVersion}, Version: {VersionNumber}",
            build.Id,
            previousVersion,
            build.VersionNumber);

        return Ok(await CreateDtoAsync(build, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var build = await _dbContext.GameBuilds
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (build is null)
        {
            return NotFound();
        }

        if (build.IsActive)
        {
            return Conflict(CreateError(
                ActiveBuildDeleteCode,
                "The active production build must be deactivated before deletion."));
        }

        var linkedInstallations = await _dbContext.Installations
            .AsNoTracking()
            .CountAsync(
                x => x.BuildVersion == build.VersionNumber,
                cancellationToken);

        var totalSessions = await _dbContext.GameSessions
            .AsNoTracking()
            .CountAsync(
                x => x.BuildVersion == build.VersionNumber,
                cancellationToken);

        if (linkedInstallations > 0 || totalSessions > 0)
        {
            return Conflict(CreateError(
                ReferencedBuildDeleteCode,
                "The build cannot be deleted while telemetry records reference its version.",
                linkedInstallations,
                totalSessions));
        }

        _dbContext.GameBuilds.Remove(build);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game build deleted. BuildId: {BuildId}, Version: {VersionNumber}",
            build.Id,
            build.VersionNumber);

        return NoContent();
    }

    private async Task DeactivateOtherBuildsAsync(
        int? buildIdToKeepActive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.GameBuilds
            .Where(x => x.IsActive);

        if (buildIdToKeepActive.HasValue)
        {
            var id = buildIdToKeepActive.Value;
            query = query.Where(x => x.Id != id);
        }

        await query.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsActive, false),
            cancellationToken);
    }

    private async Task<GameBuildDto> CreateDtoAsync(
        GameBuild build,
        CancellationToken cancellationToken)
    {
        var linkedInstallations = await _dbContext.Installations
            .AsNoTracking()
            .CountAsync(
                x => x.BuildVersion == build.VersionNumber,
                cancellationToken);

        var totalSessions = await _dbContext.GameSessions
            .AsNoTracking()
            .CountAsync(
                x => x.BuildVersion == build.VersionNumber,
                cancellationToken);

        return new GameBuildDto
        {
            Id = build.Id,
            VersionNumber = build.VersionNumber,
            ReleaseNotes = build.ReleaseNotes,
            ReleaseDateUtc = build.ReleaseDateUtc,
            IsActive = build.IsActive,
            EngineVersion = build.EngineVersion,
            LinkedInstallations = linkedInstallations,
            TotalSessions = totalSessions
        };
    }

    private static BuildOperationErrorResponse CreateError(
        string code,
        string message,
        int linkedInstallations = 0,
        int totalSessions = 0)
    {
        return new BuildOperationErrorResponse
        {
            Code = code,
            Message = message,
            LinkedInstallations = linkedInstallations,
            TotalSessions = totalSessions
        };
    }

    private static string? NormalizeReleaseNotes(string? releaseNotes)
    {
        return string.IsNullOrWhiteSpace(releaseNotes)
            ? null
            : releaseNotes.Trim();
    }
}
