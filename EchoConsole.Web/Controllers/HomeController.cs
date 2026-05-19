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

            var model = new HomeIndexViewModel
            {
                TotalSessions = overview.TotalSessions,
                ActivePlayersNow = overview.ActivePlayersNow,
                MonitoredBuilds = overview.MonitoredBuilds,
                OpenAlerts = overview.OpenAlerts,
                FeaturedBuildVersion = string.IsNullOrWhiteSpace(overview.FeaturedBuildVersion)
                    ? "N/A"
                    : overview.FeaturedBuildVersion
            };

            ViewData["Title"] = "HOME";
            ViewData["TitleI18nKey"] = "home_page_title";

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Home/Index view model.");

            var fallbackModel = new HomeIndexViewModel
            {
                TotalSessions = 0,
                ActivePlayersNow = 0,
                MonitoredBuilds = 0,
                OpenAlerts = 0,
                FeaturedBuildVersion = "N/A"
            };

            ViewData["Title"] = "HOME";
            ViewData["TitleI18nKey"] = "home_page_title";

            return View(fallbackModel);
        }
    }
}