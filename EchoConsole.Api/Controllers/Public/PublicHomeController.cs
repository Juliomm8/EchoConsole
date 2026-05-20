using EchoConsole.Api.Contracts.Public;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EchoConsole.Api.Controllers.Public;

[ApiController]
[AllowAnonymous]
[Route("api/public/home")]
public sealed class PublicHomeController : ControllerBase
{
    private const string CacheKey = "public-home-overview";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(45);

    private readonly EchoConsoleDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<PublicHomeController> _logger;

    public PublicHomeController(
        EchoConsoleDbContext dbContext,
        IMemoryCache memoryCache,
        ILogger<PublicHomeController> logger)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<PublicHomeOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _memoryCache.GetOrCreateAsync(CacheKey, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = CacheDuration;
                cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(20);

                var totalSessions = await _dbContext.GameSessions.CountAsync(cancellationToken);

                var activePlayersNow = await _dbContext.GameSessions
                    .Where(x => x.Status == SessionStatus.Active)
                    .CountAsync(cancellationToken);

                var monitoredBuilds = await _dbContext.GameBuilds.CountAsync(cancellationToken);

                var openAlerts = await _dbContext.SystemAlerts
                    .Where(x => !x.IsResolved)
                    .CountAsync(cancellationToken);

                var featuredBuildVersion = await _dbContext.GameBuilds
                    .AsNoTracking()
                    .OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => x.ReleaseDateUtc)
                    .Select(x => x.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                return new PublicHomeOverviewDto
                {
                    TotalSessions = totalSessions,
                    ActivePlayersNow = activePlayersNow,
                    MonitoredBuilds = monitoredBuilds,
                    OpenAlerts = openAlerts,
                    FeaturedBuildVersion = string.IsNullOrWhiteSpace(featuredBuildVersion)
                        ? "N/A"
                        : featuredBuildVersion
                };
            });

            return Ok(dto ?? new PublicHomeOverviewDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build public home overview.");

            return Ok(new PublicHomeOverviewDto
            {
                TotalSessions = 0,
                ActivePlayersNow = 0,
                MonitoredBuilds = 0,
                OpenAlerts = 0,
                FeaturedBuildVersion = "N/A"
            });
        }
    }
}