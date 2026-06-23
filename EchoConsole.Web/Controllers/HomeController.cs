using EchoConsole.Web.Models.Home;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    private readonly EchoConsoleHomeApiClient _homeApiClient;
    private readonly EchoConsolePatchNotesApiClient _patchNotesApiClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        EchoConsoleHomeApiClient homeApiClient,
        EchoConsolePatchNotesApiClient patchNotesApiClient,
        ILogger<HomeController> logger)
    {
        _homeApiClient = homeApiClient;
        _patchNotesApiClient = patchNotesApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        var isAuthenticated =
            User.Identity?.IsAuthenticated ?? false;

        var userAlias =
            User.FindFirst("alias")?.Value
            ?? User.Identity?.Name
            ?? "Player";

        try
        {
            var overviewTask =
                _homeApiClient.GetHomeOverviewAsync(cancellationToken);

            var patchNotesTask =
                _patchNotesApiClient.GetPublishedAsync(cancellationToken);

            await Task.WhenAll(
                overviewTask,
                patchNotesTask);

            var overview = await overviewTask;
            var patchNotes = await patchNotesTask;

            var model = new HomeIndexViewModel
            {
                TotalSessions = overview.TotalSessions,
                ActivePlayersNow = overview.ActivePlayersNow,
                MonitoredBuilds = overview.MonitoredBuilds,
                OpenAlerts = overview.OpenAlerts,
                FeaturedBuildVersion =
                    string.IsNullOrWhiteSpace(
                        overview.FeaturedBuildVersion)
                        ? "N/A"
                        : overview.FeaturedBuildVersion,

                PatchNotes = patchNotes
                    .Select(x => new PatchNoteCardViewModel
                    {
                        Id = x.Id,
                        Version = x.Version,
                        Category = x.Category,
                        Tone = x.Tone,
                        Title = x.Title,
                        Description = x.Description,
                        CreatedAtUtc = x.CreatedAtUtc
                    })
                    .ToArray(),

                IsAuthenticated = isAuthenticated,
                UserAlias = isAuthenticated
                    ? userAlias
                    : "Player"
            };

            ConfigurePageMetadata();

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to build Home/Index view model.");

            var fallbackModel = new HomeIndexViewModel
            {
                TotalSessions = 0,
                ActivePlayersNow = 0,
                MonitoredBuilds = 0,
                OpenAlerts = 0,
                FeaturedBuildVersion = "N/A",
                PatchNotes =
                    Array.Empty<PatchNoteCardViewModel>(),

                IsAuthenticated = isAuthenticated,
                UserAlias = isAuthenticated
                    ? userAlias
                    : "Player"
            };

            ConfigurePageMetadata();

            return View(fallbackModel);
        }
    }

    private void ConfigurePageMetadata()
    {
        ViewData["Title"] = "HOME";
        ViewData["TitleResourceKey"] =
            "Home_PageTitle";
    }
}
