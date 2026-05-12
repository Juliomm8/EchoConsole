using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Contracts.Common;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

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
            .OrderByDescending(x => x.ReleaseDateUtc)
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
            var activeBuilds = await _dbContext.GameBuilds
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var activeBuild in activeBuilds)
            {
                activeBuild.IsActive = false;
            }
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
            EngineVersion = entity.EngineVersion
        };

        return CreatedAtAction(
            nameof(GetAll),
            new { searchTerm = entity.VersionNumber, pageNumber = 1, pageSize = 20 },
            dto);
    }
}