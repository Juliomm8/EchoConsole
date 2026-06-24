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
        pageNumber = NormalizePageNumber(pageNumber);

        var model = await LoadIndexModelAsync(
            searchTerm,
            pageNumber,
            new CreateBuildInputModel(),
            cancellationToken);

        SetPageTitle();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "NewBuild")] CreateBuildInputModel input,
        string searchTerm = "",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        pageNumber = NormalizePageNumber(pageNumber);

        if (!ModelState.IsValid)
        {
            var invalidModel = await LoadIndexModelAsync(
                searchTerm,
                pageNumber,
                input,
                cancellationToken);

            SetPageTitle();
            return View("Index", invalidModel);
        }

        var createdBuild = await _buildsApiClient.CreateBuildAsync(
            new CreateGameBuildApiRequest
            {
                VersionNumber = input.VersionNumber.Trim(),
                ReleaseNotes = NormalizeReleaseNotes(input.ReleaseNotes),
                ReleaseDateUtc = ToUtcOffset(input.ReleaseDateUtc),
                IsActive = input.IsActive,
                EngineVersion = input.EngineVersion.Trim()
            },
            cancellationToken);

        TempData[createdBuild is null ? "BuildsError" : "BuildsSuccess"] =
            createdBuild is null
                ? "Builds_CreateError"
                : "Builds_CreateSuccess";

        return RedirectToIndex(searchTerm, pageNumber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(
        int id,
        string searchTerm = "",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var updatedBuild = await _buildsApiClient.ToggleBuildActiveAsync(
            id,
            cancellationToken);

        TempData[updatedBuild is null ? "BuildsError" : "BuildsSuccess"] =
            updatedBuild is null
                ? "Builds_StatusError"
                : "Builds_StatusUpdated";

        return RedirectToIndex(searchTerm, pageNumber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
        int id,
        bool isActive,
        string searchTerm = "",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var updated = await _buildsApiClient.SetBuildActiveAsync(
            id,
            isActive,
            cancellationToken);

        TempData[updated ? "BuildsSuccess" : "BuildsError"] =
            updated
                ? "Builds_StatusUpdated"
                : "Builds_StatusError";

        return RedirectToIndex(searchTerm, pageNumber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        [Bind(Prefix = "EditBuild")] UpdateBuildInputModel input,
        string searchTerm = "",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["BuildsError"] = "Builds_UpdateValidationError";
            return RedirectToIndex(searchTerm, pageNumber);
        }

        var updatedBuild = await _buildsApiClient.UpdateBuildAsync(
            input.Id,
            new UpdateGameBuildApiRequest
            {
                VersionNumber = input.VersionNumber.Trim(),
                ReleaseNotes = NormalizeReleaseNotes(input.ReleaseNotes),
                ReleaseDateUtc = ToUtcOffset(input.ReleaseDateUtc),
                EngineVersion = input.EngineVersion.Trim()
            },
            cancellationToken);

        TempData[updatedBuild is null ? "BuildsError" : "BuildsSuccess"] =
            updatedBuild is null
                ? "Builds_UpdateError"
                : "Builds_UpdateSuccess";

        return RedirectToIndex(searchTerm, pageNumber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        string searchTerm = "",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await _buildsApiClient.DeleteBuildAsync(
            id,
            cancellationToken);

        var isSuccess = result.Status == DeleteGameBuildApiStatus.Deleted;

        TempData[isSuccess ? "BuildsSuccess" : "BuildsError"] = result.Status switch
        {
            DeleteGameBuildApiStatus.Deleted => "Builds_DeleteSuccess",
            DeleteGameBuildApiStatus.ActiveBuild => "Builds_DeleteActiveBlocked",
            DeleteGameBuildApiStatus.HasLinkedTelemetry => "Builds_DeleteTelemetryBlocked",
            DeleteGameBuildApiStatus.NotFound => "Builds_DeleteNotFound",
            _ => "Builds_DeleteError"
        };

        return RedirectToIndex(searchTerm, pageNumber);
    }

    private async Task<BuildsIndexViewModel> LoadIndexModelAsync(
        string? searchTerm,
        int pageNumber,
        CreateBuildInputModel newBuild,
        CancellationToken cancellationToken)
    {
        try
        {
            var buildsTask = _buildsApiClient.GetBuildsAsync(
                searchTerm,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            var summaryTask = _buildsApiClient.GetSummaryAsync(cancellationToken);

            await Task.WhenAll(buildsTask, summaryTask);

            var response = await buildsTask;
            var summary = await summaryTask;

            return new BuildsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = response.PageNumber > 0 ? response.PageNumber : pageNumber,
                PageSize = response.PageSize > 0 ? response.PageSize : DefaultPageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0 ? response.TotalPages : 1,
                TotalRegisteredBuilds = summary.TotalBuilds,
                ActiveBuildVersion = summary.ActiveVersion,
                BaseEngineVersion = summary.BaseEngineVersion,
                Items = response.Items ?? Array.Empty<GameBuildApiDto>(),
                NewBuild = newBuild,
                EditBuild = new UpdateBuildInputModel()
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load builds page.");

            return new BuildsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalCount = 0,
                TotalPages = 1,
                TotalRegisteredBuilds = 0,
                Items = Array.Empty<GameBuildApiDto>(),
                NewBuild = newBuild,
                EditBuild = new UpdateBuildInputModel()
            };
        }
    }

    private IActionResult RedirectToIndex(string? searchTerm, int pageNumber)
    {
        return RedirectToAction(nameof(Index), new
        {
            searchTerm = searchTerm?.Trim() ?? string.Empty,
            pageNumber = NormalizePageNumber(pageNumber)
        });
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(
            DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static string? NormalizeReleaseNotes(string? releaseNotes)
    {
        return string.IsNullOrWhiteSpace(releaseNotes)
            ? null
            : releaseNotes.Trim();
    }

    private static int NormalizePageNumber(int pageNumber)
    {
        return pageNumber < 1 ? 1 : pageNumber;
    }

    private void SetPageTitle()
    {
        ViewData["Title"] = "GAMES AND BUILDS";
        ViewData["TitleResourceKey"] = "Builds_PageTitle";
    }
}
