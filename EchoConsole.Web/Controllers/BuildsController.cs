using EchoConsole.Web.Models.Builds;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class BuildsController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly EchoConsoleBuildsApiClient _buildsApiClient;
    private readonly ILogger<BuildsController> _logger;

    public BuildsController(
        EchoConsoleBuildsApiClient buildsApiClient,
        ILogger<BuildsController> logger)
    {
        _buildsApiClient = buildsApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string searchTerm = "",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;

        try
        {
            var response = await _buildsApiClient.GetBuildsAsync(
                searchTerm,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            var model = new BuildsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = response.PageNumber > 0 ? response.PageNumber : pageNumber,
                PageSize = response.PageSize > 0 ? response.PageSize : DefaultPageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0 ? response.TotalPages : 1,
                Items = response.Items ?? Array.Empty<GameBuildApiDto>()
            };

            ViewData["Title"] = "GAMES AND BUILDS";
            ViewData["TitleResourceKey"] = "Builds_PageTitle";

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load builds page.");

            var fallbackModel = new BuildsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalCount = 0,
                TotalPages = 1,
                Items = Array.Empty<GameBuildApiDto>()
            };

            ViewData["Title"] = "GAMES AND BUILDS";
            ViewData["TitleResourceKey"] = "Builds_PageTitle";

            return View(fallbackModel);
        }
    }
}