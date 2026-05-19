using System.Security.Claims;
using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Api.Controllers.Profile;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        EchoConsoleDbContext dbContext,
        ILogger<ProfileController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("stats")]
    public ActionResult<UserHomeStatsDto> GetStats()
    {
        var alias =
            User.FindFirst("alias")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? "Player";

        /*
         * IMPORTANT ARCHITECTURE NOTE:
         * --------------------------------
         * Current schema does NOT provide a trustworthy relationship between:
         *   - authenticated User (web identity)
         *   - GameSession / Installation (game telemetry)
         *
         * Therefore, we cannot honestly calculate personal gameplay stats yet.
         *
         * Clean future options:
         *   1) Add UserId FK to GameSession
         *   2) Add PlayerAlias or AccountId to GameSession
         *   3) Add OwnerUserId to Installation and derive stats from owned sessions
         *
         * Until that relationship exists, this endpoint returns a valid DTO with
         * the real Alias from Identity and neutral/default gameplay metrics.
         */

        var dto = new UserHomeStatsDto
        {
            Alias = alias,
            TotalSessions = 0,
            LastActivityUtc = null,
            FavoriteBuild = "N/A"
        };

        return Ok(dto);
    }
}