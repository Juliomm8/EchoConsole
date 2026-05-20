using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/installations")]
public sealed class InstallationsAdminController : ControllerBase
{
    private readonly EchoConsoleDbContext _db;

    public InstallationsAdminController(EchoConsoleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedInstallationsResponse>> GetAll(
        [FromQuery] GetInstallationsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Installations
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var likePattern = $"%{search}%";

            if (Guid.TryParse(search, out var installationId))
            {
                query = query.Where(x =>
                    x.InstallationId == installationId ||
                    EF.Functions.Like(x.DeviceName, likePattern));
            }
            else
            {
                query = query.Where(x =>
                    EF.Functions.Like(x.DeviceName, likePattern));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.LastUpdateUtc)
            .ThenBy(x => x.DeviceName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new InstallationListItemDto
            {
                InstallationId = x.InstallationId,
                DeviceName = x.DeviceName,
                DeviceModel = x.DeviceModel,
                OSVersion = x.OSVersion,
                Processor = x.Processor,
                Gpu = x.Gpu,
                RamMb = x.RamMb,
                Platform = x.Platform,
                BuildVersion = x.BuildVersion,
                Status = x.Status,
                FirstSeenUtc = x.FirstSeenUtc,
                LastUpdateUtc = x.LastUpdateUtc
            })
            .ToListAsync(cancellationToken);

        var response = new PagedInstallationsResponse
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
            Items = items
        };

        return Ok(response);
    }
}