using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/builds")]
public sealed class BuildsAdminController : ControllerBase
{
    private readonly EchoConsoleDbContext _db;

    public BuildsAdminController(EchoConsoleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedGameBuildsResponse>> GetAll(
        [FromQuery] GetGameBuildsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.GameBuilds
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var likePattern = $"%{search}%";

            query = query.Where(x =>
                EF.Functions.Like(x.VersionNumber, likePattern) ||
                EF.Functions.Like(x.EngineVersion, likePattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ReleaseDateUtc)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new GameBuildListItemDto
            {
                Id = x.Id,
                VersionNumber = x.VersionNumber,
                ReleaseNotes = x.ReleaseNotes,
                ReleaseDateUtc = x.ReleaseDateUtc,
                IsActive = x.IsActive,
                EngineVersion = x.EngineVersion
            })
            .ToListAsync(cancellationToken);

        var response = new PagedGameBuildsResponse
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            Items = items
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<GameBuildListItemDto>> Create(
        [FromBody] CreateGameBuildRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedVersion = request.VersionNumber.Trim();
        var normalizedEngineVersion = request.EngineVersion.Trim();

        var alreadyExists = await _db.GameBuilds
            .AnyAsync(x => x.VersionNumber == normalizedVersion, cancellationToken);

        if (alreadyExists)
        {
            return Conflict($"A build with version '{normalizedVersion}' already exists.");
        }

        if (request.IsActive)
        {
            var activeBuilds = await _db.GameBuilds
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

        _db.GameBuilds.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new GameBuildListItemDto
        {
            Id = entity.Id,
            VersionNumber = entity.VersionNumber,
            ReleaseNotes = entity.ReleaseNotes,
            ReleaseDateUtc = entity.ReleaseDateUtc,
            IsActive = entity.IsActive,
            EngineVersion = entity.EngineVersion
        };

        return CreatedAtAction(nameof(GetAll), new { page = 1, pageSize = 20 }, response);
    }
}