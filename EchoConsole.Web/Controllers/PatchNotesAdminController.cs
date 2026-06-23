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

    [HttpGet("create")]
    public IActionResult Create()
    {
        ConfigurePageMetadata();

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
        ConfigurePageMetadata();

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

        return RedirectToAction(nameof(Create));
    }

    private void ConfigurePageMetadata()
    {
        ViewData["Title"] =
            _localizer["PatchNotesAdmin_PageTitle"].Value;

        ViewData["TitleResourceKey"] =
            "PatchNotesAdmin_PageTitle";
    }
}
