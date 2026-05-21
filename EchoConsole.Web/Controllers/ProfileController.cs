using System.Security.Claims;
using EchoConsole.Web.Models.Profile;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly EchoConsoleProfileApiClient _profileApiClient;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        EchoConsoleProfileApiClient profileApiClient,
        ILogger<ProfileController> logger)
    {
        _profileApiClient = profileApiClient;
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
        return View(model);
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