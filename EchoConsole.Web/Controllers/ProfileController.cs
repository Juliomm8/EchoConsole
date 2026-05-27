using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using EchoConsole.Web.Models.Api.Profile;
using EchoConsole.Web.Models.Profile;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly EchoConsoleProfileApiClient _profileApiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        EchoConsoleProfileApiClient profileApiClient,
        IHttpClientFactory httpClientFactory,
        ILogger<ProfileController> logger)
    {
        _profileApiClient = profileApiClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Challenge();
        }

        var profile = await _profileApiClient.GetProfileAsync(userId, cancellationToken);

        if (profile is null)
        {
            var fallbackModel = new ProfileIndexViewModel
            {
                Alias = User.FindFirst("alias")?.Value ?? User.Identity?.Name ?? "Player",
                Role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer",
                Status = User.FindFirst("app_status")?.Value ?? "Active"
            };

            ViewData["Title"] = "PROFILE";
            ViewData["TitleI18nKey"] = "profile_page_title";

            return View(fallbackModel);
        }

        var model = new ProfileIndexViewModel
        {
            Alias = profile.Alias,
            Name = profile.Name,
            Email = profile.Email,
            AvatarKey = profile.AvatarKey,
            Theme = profile.Theme,
            Role = profile.Role,
            Status = profile.Status,
            TotalInstallations = profile.TotalInstallations,
            TotalSessions = profile.TotalSessions,
            TotalPlayTimeMinutes = profile.TotalPlayTimeMinutes,
            TotalPlayTimeLabel = FormatMinutes(profile.TotalPlayTimeMinutes),
            LastActivityLabel = profile.LastActivityUtc.HasValue
                ? FormatRelativeDate(profile.LastActivityUtc.Value)
                : "N/A",
            FavoriteBuild = string.IsNullOrWhiteSpace(profile.FavoriteBuild) ? "N/A" : profile.FavoriteBuild,
            Installations = profile.Installations
                .Select(x => new LinkedInstallationViewModel
                {
                    InstallationId = x.InstallationId,
                    DeviceName = x.DeviceName,
                    DeviceModel = x.DeviceModel,
                    Platform = x.Platform,
                    BuildVersion = x.BuildVersion,
                    Status = x.Status,
                    FirstSeenLabel = x.FirstSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    LastUpdateLabel = x.LastUpdateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                })
                .ToList()
        };

        ViewData["Title"] = "PROFILE";
        ViewData["TitleI18nKey"] = "profile_page_title";

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimInstallation(
        string installationId,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Challenge();
        }

        if (string.IsNullOrWhiteSpace(installationId))
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = "Installation ID is required.";
            return RedirectToAction(nameof(Index));
        }

        if (!Guid.TryParse(installationId.Trim(), out var parsedInstallationId))
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = "Installation ID format is invalid.";
            return RedirectToAction(nameof(Index));
        }

        var request = new ClaimInstallationRequestModel
        {
            InstallationId = parsedInstallationId,
            UserId = userId
        };

        try
        {
            var client = _httpClientFactory.CreateClient("EchoConsoleApiAdmin");

            var response = await client.PostAsJsonAsync(
                "/api/profile/installations/claim",
                request,
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<ClaimInstallationResponseModel>(
                cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                TempData["ClaimStatusType"] = "success";
                TempData["ClaimStatusMessage"] = payload?.Message ?? "Installation successfully linked.";
                return RedirectToAction(nameof(Index));
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["ClaimStatusType"] = "error";
                TempData["ClaimStatusMessage"] = payload?.Message ?? "Installation or user was not found.";
                return RedirectToAction(nameof(Index));
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                TempData["ClaimStatusType"] = "error";
                TempData["ClaimStatusMessage"] = payload?.Message ?? "This installation is already linked to another account.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogWarning(
                "Claim installation failed with status {StatusCode}. InstallationId={InstallationId}, UserId={UserId}.",
                response.StatusCode,
                parsedInstallationId,
                userId);

            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = "The platform could not link this installation right now.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to claim installation {InstallationId} for user {UserId}.",
                parsedInstallationId,
                userId);

            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = "The platform could not process your request right now.";

            return RedirectToAction(nameof(Index));
        }
    }

    private static string FormatMinutes(int totalMinutes)
    {
        if (totalMinutes <= 0)
        {
            return "0m";
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours <= 0)
        {
            return $"{minutes}m";
        }

        if (minutes <= 0)
        {
            return $"{hours}h";
        }

        return $"{hours}h {minutes}m";
    }

    private static string FormatRelativeDate(DateTimeOffset utcDate)
    {
        var diff = DateTimeOffset.UtcNow - utcDate;

        if (diff.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (diff.TotalMinutes < 60)
        {
            return $"{(int)diff.TotalMinutes}m ago";
        }

        if (diff.TotalHours < 24)
        {
            return $"{(int)diff.TotalHours}h ago";
        }

        if (diff.TotalDays < 7)
        {
            return $"{(int)diff.TotalDays}d ago";
        }

        return utcDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}