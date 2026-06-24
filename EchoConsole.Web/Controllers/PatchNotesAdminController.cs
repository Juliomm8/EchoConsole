using EchoConsole.Web.Models.PatchNotes;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/patch-notes")]
public sealed class PatchNotesAdminController : Controller
{
    private readonly EchoConsolePatchNotesApiClient _patchNotesApiClient;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<PatchNotesAdminController> _logger;

    public PatchNotesAdminController(
        EchoConsolePatchNotesApiClient patchNotesApiClient,
        IStringLocalizer<SharedResource> localizer,
        ILogger<PatchNotesAdminController> logger)
    {
        _patchNotesApiClient = patchNotesApiClient;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        ConfigurePageMetadata(
            "PatchNotesAdmin_DashboardPageTitle");

        try
        {
            var patchNotes = await _patchNotesApiClient.GetAllAsync(
                cancellationToken);

            var model = new PatchNotesAdminIndexViewModel
            {
                Items = patchNotes
                    .Select(x => new PatchNotesAdminListItemViewModel
                    {
                        Id = x.Id,
                        Version = x.Version,
                        Category = x.Category,
                        Tone = x.Tone,
                        Title = x.Title,
                        Description = x.Description,
                        CreatedAtUtc = x.CreatedAtUtc,
                        IsPublished = x.IsPublished
                    })
                    .ToArray()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load the Patch Notes CMS dashboard.");

            TempData["PatchNotesAdminError"] =
                _localizer["PatchNotesAdmin_LoadError"].Value;

            return View(new PatchNotesAdminIndexViewModel());
        }
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ConfigurePageMetadata(
            "PatchNotesAdmin_PageTitle");

        return View(new PatchNoteCreateViewModel
        {
            Tone = "green"
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PatchNoteCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        ConfigurePageMetadata(
            "PatchNotesAdmin_PageTitle");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new CreatePatchNoteApiRequest
        {
            Version = model.Version.Trim(),
            Category = model.Category.Trim(),
            Tone = model.Tone.Trim().ToLowerInvariant(),
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            IsPublished = true
        };

        var result = await _patchNotesApiClient.CreateAsync(
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Patch note creation failed for version {Version}. Error: {ErrorMessage}",
                request.Version,
                result.ErrorMessage);

            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? _localizer["PatchNotesAdmin_Error"].Value
                    : result.ErrorMessage);

            return View(model);
        }

        TempData["PatchNotesAdminSuccess"] =
            _localizer["PatchNotesAdmin_Success"].Value;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            TempData["PatchNotesAdminError"] =
                _localizer["PatchNotesAdmin_InvalidId"].Value;

            return RedirectToAction(nameof(Index));
        }

        var result = await _patchNotesApiClient.TogglePublishAsync(
            id,
            cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Patch note publication toggle failed. PatchNoteId: {PatchNoteId}, Error: {ErrorMessage}",
                id,
                result.ErrorMessage);

            TempData["PatchNotesAdminError"] =
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? _localizer["PatchNotesAdmin_ToggleError"].Value
                    : result.ErrorMessage;

            return RedirectToAction(nameof(Index));
        }

        var successKey = result.PatchNote?.IsPublished == true
            ? "PatchNotesAdmin_TogglePublishedSuccess"
            : "PatchNotesAdmin_ToggleHiddenSuccess";

        TempData["PatchNotesAdminSuccess"] =
            _localizer[successKey].Value;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            TempData["PatchNotesAdminError"] =
                _localizer["PatchNotesAdmin_InvalidId"].Value;

            return RedirectToAction(nameof(Index));
        }

        var result = await _patchNotesApiClient.DeleteAsync(
            id,
            cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Patch note deletion failed. PatchNoteId: {PatchNoteId}, Error: {ErrorMessage}",
                id,
                result.ErrorMessage);

            TempData["PatchNotesAdminError"] =
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? _localizer["PatchNotesAdmin_DeleteError"].Value
                    : result.ErrorMessage;

            return RedirectToAction(nameof(Index));
        }

        TempData["PatchNotesAdminSuccess"] =
            _localizer["PatchNotesAdmin_DeleteSuccess"].Value;

        return RedirectToAction(nameof(Index));
    }

    private void ConfigurePageMetadata(
        string resourceKey)
    {
        ViewData["Title"] =
            _localizer[resourceKey].Value;

        ViewData["TitleResourceKey"] =
            resourceKey;
    }
}
