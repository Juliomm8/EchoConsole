using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using EchoConsole.Web.Models.Api.Profile;
using EchoConsole.Web.Models.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IHttpClientFactory httpClientFactory,
        ILogger<ProfileController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new ClaimInstallationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimInstallation(
        ClaimInstallationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Authenticated user is missing a valid NameIdentifier claim.");
            model.IsSuccess = false;
            model.StatusMessage = "Your session does not contain a valid user identifier.";
            return View("Index", model);
        }

        if (!Guid.TryParse(model.InstallationId, out var installationId))
        {
            ModelState.AddModelError(nameof(model.InstallationId), "Installation ID format is invalid.");
            return View("Index", model);
        }

        var request = new ClaimInstallationRequestModel
        {
            InstallationId = installationId,
            UserId = userId
        };

        try
        {
            var client = _httpClientFactory.CreateClient("EchoConsoleApiAdmin");

            var response = await client.PostAsJsonAsync(
                "/api/profile/installations/claim",
                request,
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<ClaimInstallationResponseModel>(cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                model.IsSuccess = true;
                model.StatusMessage = payload?.Message ?? "Installation successfully linked.";
                return View("Index", model);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                model.IsSuccess = false;
                model.StatusMessage = payload?.Message ?? "Installation or user not found.";
                return View("Index", model);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                model.IsSuccess = false;
                model.StatusMessage = payload?.Message ?? "This installation is already linked to another account.";
                return View("Index", model);
            }

            model.IsSuccess = false;
            model.StatusMessage = "An unexpected error occurred while claiming the installation.";
            return View("Index", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to claim installation {InstallationId} for user {UserId}.", installationId, userId);
            model.IsSuccess = false;
            model.StatusMessage = "The platform could not process your request right now.";
            return View("Index", model);
        }
    }
}