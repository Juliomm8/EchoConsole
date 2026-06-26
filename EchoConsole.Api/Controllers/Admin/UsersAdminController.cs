using EchoConsole.Api.Contracts.Admin;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/users")]
public sealed class UsersAdminController : ControllerBase
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<UsersAdminController> _logger;

    public UsersAdminController(
        EchoConsoleDbContext dbContext,
        ILogger<UsersAdminController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedUsersResponse>> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var trimmedSearch = searchTerm?.Trim();
        var normalizedSearch = string.IsNullOrWhiteSpace(trimmedSearch)
            ? null
            : trimmedSearch[..Math.Min(trimmedSearch.Length, 100)];

        var baseQuery = _dbContext.Users
            .AsNoTracking();

        if (normalizedSearch is not null)
        {
            var pattern = $"{normalizedSearch}%";

            baseQuery = baseQuery.Where(user =>
                EF.Functions.Like(user.Name, pattern) ||
                EF.Functions.Like(user.Email!, pattern));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var roleCounts = await _dbContext.Users
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                AdminCount = group.Count(user => user.Role == UserRole.Admin),
                ViewerCount = group.Count(user => user.Role == UserRole.Viewer)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var users = await baseQuery
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Email)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserListItemDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                Role = user.Role.ToString(),
                Status = user.Status.ToString(),
                CreatedAtUtc = user.CreatedAtUtc,
                InstallationCount = user.Installations.Count,
                LastTelemetryUtc = user.Installations
                    .Select(installation => (DateTimeOffset?)installation.LastUpdateUtc)
                    .Max()
            })
            .ToListAsync(cancellationToken);

        var userIds = users
            .Select(user => user.Id)
            .ToArray();

        var hardware = userIds.Length == 0
            ? new List<UserInstallationProjection>()
            : await _dbContext.Installations
                .AsNoTracking()
                .Where(installation =>
                    installation.OwnerUserId.HasValue &&
                    userIds.Contains(installation.OwnerUserId.Value))
                .OrderByDescending(installation => installation.LastUpdateUtc)
                .Select(installation => new UserInstallationProjection
                {
                    OwnerUserId = installation.OwnerUserId!.Value,
                    InstallationId = installation.InstallationId,
                    DeviceName = installation.DeviceName,
                    Cpu = installation.Processor,
                    Gpu = installation.Gpu,
                    RamMb = installation.RamMb,
                    OSVersion = installation.OSVersion,
                    Platform = installation.Platform,
                    BuildVersion = installation.BuildVersion,
                    AdminStatus = installation.AdminStatus,
                    LastUpdateUtc = installation.LastUpdateUtc,
                    LastSession = installation.Sessions
                        .OrderByDescending(session => session.LastHeartbeatUtc)
                        .ThenByDescending(session => session.Id)
                        .Select(session => new UserLastSessionProjection
                        {
                            SessionId = session.SessionId,
                            Status = session.Status,
                            DurationMinutes = EF.Functions.DateDiffMinute(
                                session.StartedAtUtc,
                                session.LastHeartbeatUtc)
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

        var hardwareByUser = hardware
            .GroupBy(item => item.OwnerUserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UserInstallationHardwareDto>)group
                    .Select(item => new UserInstallationHardwareDto
                    {
                        InstallationId = item.InstallationId,
                        DeviceName = item.DeviceName,
                        Cpu = item.Cpu,
                        Gpu = item.Gpu,
                        RamMb = item.RamMb,
                        OSVersion = item.OSVersion,
                        Platform = item.Platform,
                        BuildVersion = item.BuildVersion,
                        AdminStatus = item.AdminStatus,
                        LastUpdateUtc = item.LastUpdateUtc,
                        LastSessionId = item.LastSession?.SessionId,
                        LastSessionStatus = item.LastSession?.Status.ToString(),
                        LastSessionDurationMinutes = item.LastSession is null
                            ? null
                            : Math.Max(0, item.LastSession.DurationMinutes)
                    })
                    .ToArray());

        foreach (var user in users)
        {
            user.Installations = hardwareByUser.TryGetValue(
                user.Id,
                out var installations)
                ? installations
                : Array.Empty<UserInstallationHardwareDto>();
        }

        var response = new PagedUsersResponse
        {
            Items = users,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalCount / (double)pageSize)),
            AdminCount = roleCounts?.AdminCount ?? 0,
            ViewerCount = roleCounts?.ViewerCount ?? 0
        };

        _logger.LogInformation(
            "Users NOC list requested. SearchTerm={SearchTerm}, Page={PageNumber}, PageSize={PageSize}, Total={TotalCount}",
            normalizedSearch,
            pageNumber,
            pageSize,
            totalCount);

        return Ok(response);
    }

    private sealed class UserInstallationProjection
    {
        public int OwnerUserId { get; set; }
        public Guid InstallationId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string? Cpu { get; set; }
        public string? Gpu { get; set; }
        public int? RamMb { get; set; }
        public string OSVersion { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string BuildVersion { get; set; } = string.Empty;
        public string AdminStatus { get; set; } = string.Empty;
        public DateTimeOffset LastUpdateUtc { get; set; }
        public UserLastSessionProjection? LastSession { get; set; }
    }

    private sealed class UserLastSessionProjection
    {
        public Guid SessionId { get; set; }
        public SessionStatus Status { get; set; }
        public int DurationMinutes { get; set; }
    }
}
