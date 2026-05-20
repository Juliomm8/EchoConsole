using EchoConsole.Web.Models.Home;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    private readonly EchoConsoleHomeApiClient _homeApiClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        EchoConsoleHomeApiClient homeApiClient,
        ILogger<HomeController> logger)
    {
        _homeApiClient = homeApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            var overview = await _homeApiClient.GetHomeOverviewAsync(cancellationToken);

            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var userAlias =
                User.FindFirst("alias")?.Value
                ?? User.Identity?.Name
                ?? "Player";

            var model = new HomeIndexViewModel
            {
                TotalSessions = overview.TotalSessions,
                ActivePlayersNow = overview.ActivePlayersNow,
                MonitoredBuilds = overview.MonitoredBuilds,
                OpenAlerts = overview.OpenAlerts,
                FeaturedBuildVersion = string.IsNullOrWhiteSpace(overview.FeaturedBuildVersion)
                    ? "N/A"
                    : overview.FeaturedBuildVersion,

                IsAuthenticated = isAuthenticated,
                UserAlias = isAuthenticated ? userAlias : "Player",
                UserTotalSessions = isAuthenticated ? 0 : 0,
                UserLastActivity = isAuthenticated ? "N/A" : "N/A",
                UserFavoriteBuild = isAuthenticated ? "N/A" : "N/A"
            };

            ViewData["Title"] = "HOME";
            ViewData["TitleI18nKey"] = "home_page_title";

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Home/Index view model.");

            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var userAlias =
                User.FindFirst("alias")?.Value
                ?? User.Identity?.Name
                ?? "Player";

            var fallbackModel = new HomeIndexViewModel
            {
                TotalSessions = 0,
                ActivePlayersNow = 0,
                MonitoredBuilds = 0,
                OpenAlerts = 0,
                FeaturedBuildVersion = "N/A",

                IsAuthenticated = isAuthenticated,
                UserAlias = isAuthenticated ? userAlias : "Player",
                UserTotalSessions = 0,
                UserLastActivity = "N/A",
                UserFavoriteBuild = "N/A"
            };

            ViewData["Title"] = "HOME";
            ViewData["TitleI18nKey"] = "home_page_title";

            return View(fallbackModel);
        }
    }
}