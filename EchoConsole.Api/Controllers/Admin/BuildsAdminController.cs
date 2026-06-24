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

        var response = new PagedResponse<GameBuildDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Ok(response);
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

        var existing = await _dbContext.GameBuilds
            .AnyAsync(x => x.VersionNumber == normalizedVersion, cancellationToken);

        if (existing)
        {
            return Conflict(new
            {
                message = $"A build with version '{normalizedVersion}' already exists."
            });
        }

        if (request.IsActive)
        {
            await _dbContext.GameBuilds
                .Where(x => x.IsActive)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.IsActive, false),
                    cancellationToken);
        }

        var entity = new GameBuild
        {
            VersionNumber = normalizedVersion,
            ReleaseNotes = string.IsNullOrWhiteSpace(request.ReleaseNotes)
                ? null
                : request.ReleaseNotes.Trim(),
            ReleaseDateUtc = request.ReleaseDateUtc,
            IsActive = request.IsActive,
            EngineVersion = normalizedEngineVersion
        };

        _dbContext.GameBuilds.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "New game build created. Version: {VersionNumber}, EngineVersion: {EngineVersion}, IsActive: {IsActive}",
            entity.VersionNumber,
            entity.EngineVersion,
            entity.IsActive);

        var dto = new GameBuildDto
        {
            Id = entity.Id,
            VersionNumber = entity.VersionNumber,
            ReleaseNotes = entity.ReleaseNotes,
            ReleaseDateUtc = entity.ReleaseDateUtc,
            IsActive = entity.IsActive,
            EngineVersion = entity.EngineVersion,
            LinkedInstallations = 0,
            TotalSessions = 0
        };

        return CreatedAtAction(
            nameof(GetAll),
            new { searchTerm = entity.VersionNumber, pageNumber = 1, pageSize = 20 },
            dto);
    }

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<GameBuildDto>> SetActive(
        int id,
        [FromBody] SetGameBuildActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var build = await _dbContext.GameBuilds
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (build is null)
        {
            return NotFound();
        }

        if (request.IsActive)
        {
            await _dbContext.GameBuilds
                .Where(x => x.IsActive && x.Id != id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.IsActive, false),
                    cancellationToken);
        }

        build.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        _logger.LogInformation(
            "Game build active status updated. BuildId: {BuildId}, Version: {VersionNumber}, IsActive: {IsActive}",
            build.Id,
            build.VersionNumber,
            build.IsActive);

        return Ok(new GameBuildDto
        {
            Id = build.Id,
            VersionNumber = build.VersionNumber,
            ReleaseNotes = build.ReleaseNotes,
            ReleaseDateUtc = build.ReleaseDateUtc,
            IsActive = build.IsActive,
            EngineVersion = build.EngineVersion,
            LinkedInstallations = linkedInstallations,
            TotalSessions = totalSessions
        });
    }
}
