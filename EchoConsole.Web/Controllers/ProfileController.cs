using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Web.Models.Api.Profile;
using EchoConsole.Web.Models.Profile;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly EchoConsoleProfileApiClient _profileApiClient;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        EchoConsoleProfileApiClient profileApiClient,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<ProfileController> logger)
    {
        _profileApiClient = profileApiClient;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var profile = await _profileApiClient.GetProfileAsync(userId.Value, cancellationToken);

        if (profile is null)
        {
            var fallbackModel = new ProfileIndexViewModel
            {
                Alias = User.FindFirst("alias")?.Value ?? User.Identity?.Name ?? "Player",
                Role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer",
                Status = User.FindFirst("app_status")?.Value ?? "Active",
                Installations = Array.Empty<LinkedInstallationViewModel>()
            };

            ViewData["Title"] = "PROFILE";
            ViewData["TitleI18nKey"] = "profile_page_title";

            return View(fallbackModel);
        }

        var model = MapProfileToViewModel(profile);

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
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        if (!TryParseInstallationId(installationId, out var parsedInstallationId))
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = "Installation ID format is invalid.";
            return RedirectToAction(nameof(Index));
        }

        var request = new ClaimInstallationRequestModel
        {
            InstallationId = parsedInstallationId,
            UserId = userId.Value
        };

        var result = await _profileApiClient.ClaimInstallationAsync(request, cancellationToken);

        if (result?.Success == true)
        {
            TempData["ClaimStatusType"] = "success";
            TempData["ClaimStatusMessage"] = result.Message;
        }
        else
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = result?.Message ?? "The platform could not link this installation right now.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkInstallation(
        string installationId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        if (!TryParseInstallationId(installationId, out var parsedInstallationId))
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = "Installation ID format is invalid.";
            return RedirectToAction(nameof(Index));
        }

        var request = new UnlinkInstallationRequestModel
        {
            InstallationId = parsedInstallationId,
            UserId = userId.Value
        };

        var result = await _profileApiClient.UnlinkInstallationAsync(request, cancellationToken);

        if (result?.Success == true)
        {
            TempData["ClaimStatusType"] = "success";
            TempData["ClaimStatusMessage"] = result.Message;
        }
        else
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] = result?.Message ?? "The platform could not unlink this installation right now.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(
        UpdateProfileFormModel form,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["ProfileStatusType"] = "error";
            TempData["ProfileStatusMessage"] = "Check the profile fields and try again.";
            return RedirectToAction(nameof(Index));
        }

        var request = new UpdateProfileRequestModel
        {
            Alias = form.Alias.Trim(),
            AvatarKey = form.AvatarKey.Trim(),
            Theme = form.Theme.Trim()
        };

        var result = await _profileApiClient.UpdateProfileAsync(
            userId.Value,
            request,
            cancellationToken);

        if (result?.Success != true)
        {
            TempData["ProfileStatusType"] = "error";
            TempData["ProfileStatusMessage"] = result?.Message ?? "The platform could not update your profile.";
            return RedirectToAction(nameof(Index));
        }

        var refreshedUser = await _userManager.FindByIdAsync(userId.Value.ToString());

        if (refreshedUser is null)
        {
            TempData["ProfileStatusType"] = "error";
            TempData["ProfileStatusMessage"] = "Profile updated, but the current session could not be refreshed.";
            return RedirectToAction(nameof(Index));
        }

        var principal = await _signInManager.CreateUserPrincipalAsync(refreshedUser);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal);

        TempData["ProfileStatusType"] = "success";
        TempData["ProfileStatusMessage"] = result.Message;

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Profile/Sessions")]
    public async Task<IActionResult> Sessions(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        var result = await _profileApiClient.GetSessionHistoryAsync(
            userId.Value,
            page,
            pageSize,
            cancellationToken);

        var model = result is null
            ? new ProfileSessionHistoryViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 1,
                HasPreviousPage = false,
                HasNextPage = false,
                Items = Array.Empty<ProfileSessionHistoryRowViewModel>()
            }
            : new ProfileSessionHistoryViewModel
            {
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                HasPreviousPage = result.HasPreviousPage,
                HasNextPage = result.HasNextPage,
                Items = result.Items.Select(x => new ProfileSessionHistoryRowViewModel
                {
                    SessionId = x.SessionId,
                    InstallationId = x.InstallationId,
                    DeviceName = string.IsNullOrWhiteSpace(x.DeviceName) ? "-" : x.DeviceName,
                    BuildVersion = string.IsNullOrWhiteSpace(x.BuildVersion) ? "-" : x.BuildVersion,
                    CurrentScene = string.IsNullOrWhiteSpace(x.CurrentScene) ? "-" : x.CurrentScene,
                    CurrentPhase = string.IsNullOrWhiteSpace(x.CurrentPhase) ? "-" : x.CurrentPhase,
                    StatusLabel = string.IsNullOrWhiteSpace(x.StatusLabel) ? "-" : x.StatusLabel,
                    IsLive = x.IsLive,
                    StartedAtLabel = FormatDateTime(x.StartedAtUtc),
                    DurationLabel = FormatMinutes(x.DurationMinutes),
                    LastHeartbeatLabel = FormatRelativeDate(x.LastHeartbeatUtc)
                }).ToList()
            };

        ViewData["Title"] = "SESSION HISTORY";
        ViewData["TitleI18nKey"] = "profile_sessions_page_title";

        return View(model);
    }

    [HttpGet("Profile/Sessions/{id:guid}")]
    public async Task<IActionResult> SessionDetail(
    Guid id,
    CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var detail = await _profileApiClient.GetSessionDetailAsync(
            userId.Value,
            id,
            cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        var model = new ProfileSessionDetailViewModel
        {
            SessionId = detail.SessionId,
            InstallationId = detail.InstallationId,
            DeviceName = string.IsNullOrWhiteSpace(detail.DeviceName) ? "-" : detail.DeviceName,
            DeviceModel = string.IsNullOrWhiteSpace(detail.DeviceModel) ? "-" : detail.DeviceModel,
            Platform = string.IsNullOrWhiteSpace(detail.Platform) ? "-" : detail.Platform,
            BuildVersion = string.IsNullOrWhiteSpace(detail.BuildVersion) ? "-" : detail.BuildVersion,
            CurrentScene = string.IsNullOrWhiteSpace(detail.CurrentScene) ? "-" : detail.CurrentScene,
            CurrentGameState = string.IsNullOrWhiteSpace(detail.CurrentGameState) ? "-" : detail.CurrentGameState,
            CurrentPhase = string.IsNullOrWhiteSpace(detail.CurrentPhase) ? "-" : detail.CurrentPhase,
            StatusLabel = string.IsNullOrWhiteSpace(detail.StatusLabel) ? "-" : detail.StatusLabel,
            IsLive = detail.IsLive,
            StartedAtLabel = FormatDateTime(detail.StartedAtUtc),
            EndedAtLabel = detail.EndedAtUtc.HasValue ? FormatDateTime(detail.EndedAtUtc.Value) : "Not ended",
            LastHeartbeatLabel = FormatDateTime(detail.LastHeartbeatUtc),
            DurationLabel = FormatMinutes(detail.DurationMinutes),
            Events = detail.Events.Select(x => new ProfileSessionEventTimelineItemViewModel
            {
                Id = x.Id,
                EventType = string.IsNullOrWhiteSpace(x.EventType) ? "-" : x.EventType,
                Scene = string.IsNullOrWhiteSpace(x.Scene) ? "-" : x.Scene,
                GameState = string.IsNullOrWhiteSpace(x.GameState) ? "-" : x.GameState,
                Phase = string.IsNullOrWhiteSpace(x.Phase) ? "-" : x.Phase,
                PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson) ? string.Empty : x.PayloadJson,
                HasPayload = !string.IsNullOrWhiteSpace(x.PayloadJson),
                ClientTimeLabel = x.ClientTimeUtc.HasValue ? FormatDateTime(x.ClientTimeUtc.Value) : "Not provided",
                CreatedAtLabel = FormatDateTime(x.CreatedAtUtc)
            }).ToList()
        };

        ViewData["Title"] = "SESSION DETAIL";
        ViewData["TitleI18nKey"] = "profile_session_detail_page_title";

        return View("SessionDetail", model);
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdClaim, out var userId)
            ? userId
            : null;
    }

    private static bool TryParseInstallationId(string? installationId, out Guid parsedInstallationId)
    {
        parsedInstallationId = Guid.Empty;

        return !string.IsNullOrWhiteSpace(installationId)
            && Guid.TryParse(installationId.Trim(), out parsedInstallationId);
    }

    private static ProfileIndexViewModel MapProfileToViewModel(UserProfileApiModel profile)
    {
        return new ProfileIndexViewModel
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
                    DeviceName = string.IsNullOrWhiteSpace(x.DeviceName) ? "-" : x.DeviceName,
                    DeviceModel = string.IsNullOrWhiteSpace(x.DeviceModel) ? "-" : x.DeviceModel,
                    Platform = string.IsNullOrWhiteSpace(x.Platform) ? "-" : x.Platform,
                    BuildVersion = string.IsNullOrWhiteSpace(x.BuildVersion) ? "-" : x.BuildVersion,
                    Status = string.IsNullOrWhiteSpace(x.Status) ? "-" : x.Status,
                    FirstSeenLabel = x.FirstSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    LastUpdateLabel = x.LastUpdateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                })
                .ToList()
        };
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

    private static string FormatDateTime(DateTimeOffset utcDate)
    {
        return utcDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}