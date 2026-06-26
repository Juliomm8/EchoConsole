using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Web.Models.Api.Profile;
using EchoConsole.Web.Models.Profile;
using EchoConsole.Web.Security;
using EchoConsole.Web.Services.Api;
using EchoConsole.Web.Services.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private static readonly TimeSpan ActiveHeartbeatWindow =
        TimeSpan.FromSeconds(90);

    private readonly EchoConsoleDbContext _dbContext;
    private readonly EchoConsoleProfileApiClient _profileApiClient;
    private readonly UserManager<User> _userManager;
    private readonly IUserSessionService _userSessionService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        EchoConsoleDbContext dbContext,
        EchoConsoleProfileApiClient profileApiClient,
        UserManager<User> userManager,
        IUserSessionService userSessionService,
        TimeProvider timeProvider,
        ILogger<ProfileController> logger)
    {
        _dbContext = dbContext;
        _profileApiClient = profileApiClient;
        _userManager = userManager;
        _userSessionService = userSessionService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string section = "identity",
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => new
            {
                x.Id,
                x.Alias,
                x.Name,
                x.Email,
                x.EmailConfirmed,
                x.AvatarKey,
                x.Theme,
                x.PreferredLanguage,
                x.Role,
                x.Status,
                x.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Challenge();
        }

        var normalizedSection = NormalizeSection(section);

        var identityPreview = new IdentityTabViewModel
        {
            UserId = user.Id,
            Alias = user.Alias,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            AvatarKey = user.AvatarKey,
            Theme = user.Theme,
            PreferredLanguage = user.PreferredLanguage,
            Role = user.Role.ToString(),
            RoleDisplayName = ProfileCatalog.GetRoleDisplayName(user.Role),
            Status = user.Status.ToString(),
            CreatedAtUtc = user.CreatedAtUtc
        };

        var model = new ProfileViewModel
        {
            InitialSection = normalizedSection,
            IdentityPreview = identityPreview,
            Alias = identityPreview.Alias,
            Name = identityPreview.Name,
            Email = identityPreview.Email,
            AvatarKey = identityPreview.AvatarKey,
            Theme = identityPreview.Theme,
            Role = identityPreview.Role,
            Status = identityPreview.Status,
            Installations = Array.Empty<LinkedInstallationViewModel>()
        };

        ViewData["Title"] = "PROFILE";
        ViewData["TitleResourceKey"] = "Profile_PageTitle";

        return View(model);
    }

    [HttpGet("Profile/Api/Identity")]
    public async Task<IActionResult> GetIdentityAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => new
            {
                x.Id,
                x.Alias,
                x.Name,
                x.Email,
                x.EmailConfirmed,
                x.AvatarKey,
                x.Theme,
                x.PreferredLanguage,
                x.Role,
                x.Status,
                x.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                success = false,
                code = "user_not_found",
                message = "The player record could not be found."
            });
        }

        var installationIds = await _dbContext.Installations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == user.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var sessionRows = installationIds.Count == 0
            ? new List<PlayerSessionSummaryRow>()
            : await _dbContext.GameSessions
                .AsNoTracking()
                .Where(x => installationIds.Contains(x.InstallationDbId))
                .Select(x => new PlayerSessionSummaryRow
                {
                    StartedAtUtc = x.StartedAtUtc,
                    EndedAtUtc = x.EndedAtUtc,
                    LastHeartbeatUtc = x.LastHeartbeatUtc,
                    BuildVersion = x.BuildVersion
                })
                .ToListAsync(cancellationToken);

        long totalPlayTimeMinutes = 0;
        var longestSessionMinutes = 0;
        DateTimeOffset? lastActivityUtc = null;

        foreach (var session in sessionRows)
        {
            var endUtc = session.EndedAtUtc ?? session.LastHeartbeatUtc;
            var duration = endUtc - session.StartedAtUtc;
            var durationMinutes = duration > TimeSpan.Zero
                ? (int)Math.Floor(duration.TotalMinutes)
                : 0;

            totalPlayTimeMinutes += durationMinutes;
            longestSessionMinutes = Math.Max(
                longestSessionMinutes,
                durationMinutes);

            if (!lastActivityUtc.HasValue ||
                session.LastHeartbeatUtc > lastActivityUtc.Value)
            {
                lastActivityUtc = session.LastHeartbeatUtc;
            }
        }

        var favoriteBuild = sessionRows
            .Where(x => !string.IsNullOrWhiteSpace(x.BuildVersion))
            .GroupBy(x => x.BuildVersion, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "N/A";

        var model = new IdentityTabViewModel
        {
            UserId = user.Id,
            Alias = user.Alias,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            AvatarKey = user.AvatarKey,
            Theme = user.Theme,
            PreferredLanguage = user.PreferredLanguage,
            Role = user.Role.ToString(),
            RoleDisplayName = ProfileCatalog.GetRoleDisplayName(user.Role),
            Status = user.Status.ToString(),
            CreatedAtUtc = user.CreatedAtUtc,
            LastActivityUtc = lastActivityUtc,
            LinkedNodeCount = installationIds.Count,
            TotalSessions = sessionRows.Count,
            TotalPlayTimeMinutes = totalPlayTimeMinutes,
            TotalPlayTimeHours = Math.Round(
                totalPlayTimeMinutes / 60d,
                1,
                MidpointRounding.AwayFromZero),
            LongestSessionMinutes = longestSessionMinutes,
            FavoriteBuild = favoriteBuild
        };

        return Json(new
        {
            success = true,
            data = model
        });
    }

    [HttpGet("Profile/Api/Security/Sessions")]
    public async Task<IActionResult> GetActiveSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        await _userSessionService.EnsureCurrentSessionAsync(
            HttpContext,
            user,
            cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var currentSessionKeyHash =
            _userSessionService.GetCurrentSessionKeyHash(User);

        var rows = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(x =>
                x.UserId == user.Id &&
                !x.RevokedAtUtc.HasValue &&
                x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Select(x => new
            {
                x.Id,
                x.SessionKeyHash,
                x.UserAgent,
                x.MaskedIpAddress,
                x.CreatedAtUtc,
                x.LastSeenAtUtc,
                x.ExpiresAtUtc
            })
            .ToListAsync(cancellationToken);

        var sessions = rows
            .Select(row =>
            {
                var descriptor = UserAgentParser.Parse(row.UserAgent);

                return new ActiveUserSessionViewModel
                {
                    Id = row.Id,
                    Browser = descriptor.Browser,
                    OperatingSystem = descriptor.OperatingSystem,
                    DeviceLabel = descriptor.DeviceLabel,
                    MaskedIpAddress = row.MaskedIpAddress,
                    CreatedAtUtc = row.CreatedAtUtc,
                    LastSeenAtUtc = row.LastSeenAtUtc,
                    ExpiresAtUtc = row.ExpiresAtUtc,
                    IsCurrent = string.Equals(
                        row.SessionKeyHash,
                        currentSessionKeyHash,
                        StringComparison.Ordinal)
                };
            })
            .ToList();

        var model = new SecurityTabViewModel
        {
            HasLocalPassword = await _userManager.HasPasswordAsync(user),
            EmailConfirmed = user.EmailConfirmed,
            ActiveSessionCount = sessions.Count,
            ActiveSessions = sessions
        };

        return Json(new
        {
            success = true,
            data = model
        });
    }

    [HttpGet("Profile/Api/Fleet")]
    public async Task<IActionResult> GetFleetAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var installations = await _dbContext.Installations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId.Value)
            .OrderByDescending(x => x.LastUpdateUtc)
            .Select(x => new
            {
                x.Id,
                x.InstallationId,
                x.AdminAlias,
                x.DeviceName,
                x.DeviceModel,
                x.Platform,
                x.OSVersion,
                x.BuildVersion,
                x.Status,
                x.FirstSeenUtc,
                x.LastUpdateUtc
            })
            .ToListAsync(cancellationToken);

        var installationDbIds = installations
            .Select(x => x.Id)
            .ToList();

        var activeSessionRows = installationDbIds.Count == 0
            ? new List<ActiveFleetSessionRow>()
            : await _dbContext.GameSessions
                .AsNoTracking()
                .Where(x =>
                    installationDbIds.Contains(x.InstallationDbId) &&
                    x.Status == SessionStatus.Active &&
                    !x.EndedAtUtc.HasValue)
                .OrderByDescending(x => x.LastHeartbeatUtc)
                .Select(x => new ActiveFleetSessionRow
                {
                    InstallationDbId = x.InstallationDbId,
                    CurrentScene = x.CurrentScene,
                    LastHeartbeatUtc = x.LastHeartbeatUtc
                })
                .ToListAsync(cancellationToken);

        var latestSessionByInstallation = activeSessionRows
            .GroupBy(x => x.InstallationDbId)
            .ToDictionary(
                group => group.Key,
                group => group.First());

        var activeCutoff = _timeProvider
            .GetUtcNow()
            .Subtract(ActiveHeartbeatWindow);

        var devices = installations
            .Select(installation =>
            {
                latestSessionByInstallation.TryGetValue(
                    installation.Id,
                    out var liveSession);

                var hasLiveHeartbeat = liveSession is not null &&
                    liveSession.LastHeartbeatUtc >= activeCutoff;

                return new FleetDeviceViewModel
                {
                    InstallationId = installation.InstallationId,
                    DisplayName = string.IsNullOrWhiteSpace(
                        installation.AdminAlias)
                        ? installation.DeviceName
                        : installation.AdminAlias,
                    DeviceName = installation.DeviceName,
                    DeviceModel = installation.DeviceModel,
                    Platform = installation.Platform,
                    OperatingSystem = installation.OSVersion,
                    BuildVersion = installation.BuildVersion,
                    TelemetryStatus = hasLiveHeartbeat
                        ? "Active"
                        : installation.Status,
                    CurrentScene = hasLiveHeartbeat &&
                        !string.IsNullOrWhiteSpace(liveSession!.CurrentScene)
                            ? liveSession.CurrentScene
                            : "N/A",
                    FirstSeenUtc = installation.FirstSeenUtc,
                    LastUpdateUtc = installation.LastUpdateUtc
                };
            })
            .ToList();

        var model = new FleetTabViewModel
        {
            LinkedDeviceCount = devices.Count,
            Devices = devices
        };

        return Json(new
        {
            success = true,
            data = model
        });
    }

    [HttpPost("Profile/Api/Identity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateIdentityAsync(
        [FromBody] UpdateIdentityViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationResponse());
        }

        var alias = model.Alias.Trim();
        var avatarKey = ProfileCatalog.NormalizeAvatarKey(
            model.AvatarKey);
        var theme = ProfileCatalog.NormalizeTheme(model.Theme);
        var preferredLanguage = ProfileCatalog.NormalizeLanguage(
            model.PreferredLanguage);

        if (!ProfileCatalog.IsValidAlias(alias))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_alias",
                message = "Alias must contain 3 to 32 letters, numbers, spaces, hyphens or underscores."
            });
        }

        if (!ProfileCatalog.IsAllowedAvatarKey(avatarKey))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_avatar",
                message = "The selected avatar does not belong to the approved catalog."
            });
        }

        if (!ProfileCatalog.IsAllowedTheme(theme))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_theme",
                message = "The selected terminal theme does not belong to the approved catalog."
            });
        }

        if (!ProfileCatalog.IsAllowedLanguage(preferredLanguage))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_language",
                message = "The selected language is not supported."
            });
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var normalizedAlias = alias.ToUpperInvariant();

        var aliasAlreadyExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != user.Id &&
                     x.Alias.ToUpper() == normalizedAlias,
                cancellationToken);

        if (aliasAlreadyExists)
        {
            return Conflict(new
            {
                success = false,
                code = "alias_already_used",
                message = "The selected alias is already assigned to another player."
            });
        }

        user.Alias = alias;
        user.AvatarKey = avatarKey;
        user.Theme = theme;
        user.PreferredLanguage = preferredLanguage;
        user.ProfileUpdatedAtUtc = _timeProvider.GetUtcNow();

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                success = false,
                code = "identity_update_failed",
                message = "The player identity could not be updated.",
                errors = updateResult.Errors.Select(x => x.Description)
            });
        }

        await _userSessionService.RefreshCurrentPrincipalAsync(
            HttpContext,
            user,
            cancellationToken);

        _logger.LogInformation(
            "Player identity updated. UserId={UserId}, Alias={Alias}, Avatar={AvatarKey}, Theme={Theme}, Language={Language}.",
            user.Id,
            user.Alias,
            user.AvatarKey,
            user.Theme,
            user.PreferredLanguage);

        return Json(new
        {
            success = true,
            message = "Player identity synchronized.",
            data = new
            {
                user.Alias,
                user.AvatarKey,
                user.Theme,
                user.PreferredLanguage,
                profileUpdatedAtUtc = user.ProfileUpdatedAtUtc
            }
        });
    }

    [HttpPost("Profile/Api/Security/Password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationResponse());
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (!await _userManager.HasPasswordAsync(user))
        {
            return BadRequest(new
            {
                success = false,
                code = "local_password_not_configured",
                message = "This account uses an external provider and does not have a local password yet."
            });
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                success = false,
                code = "password_change_failed",
                message = "The password could not be changed.",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        var revokedSessions = await _userSessionService
            .RevokeOtherSessionsAndRefreshAsync(
                HttpContext,
                user,
                "PasswordChanged",
                updateSecurityStamp: false,
                cancellationToken);

        _logger.LogInformation(
            "Password changed for UserId={UserId}. RevokedSessions={RevokedSessions}.",
            user.Id,
            revokedSessions);

        return Json(new
        {
            success = true,
            message = "Credentials rotated successfully.",
            revokedSessions
        });
    }

    [HttpPost("Profile/Api/Security/Terminate-Others")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateAllOtherSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var revokedSessions = await _userSessionService
            .RevokeOtherSessionsAndRefreshAsync(
                HttpContext,
                user,
                "TerminatedByUser",
                updateSecurityStamp: true,
                cancellationToken);

        return Json(new
        {
            success = true,
            message = revokedSessions == 1
                ? "One remote session was terminated."
                : $"{revokedSessions} remote sessions were terminated.",
            revokedSessions
        });
    }

    [HttpPost("Profile/Api/Fleet/Unlink")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkDeviceAsync(
        [FromBody] UnlinkDeviceViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid || model.InstallationId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_installation_id",
                message = "A valid installation identifier is required."
            });
        }

        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var installation = await _dbContext.Installations
            .FirstOrDefaultAsync(
                x => x.InstallationId == model.InstallationId,
                cancellationToken);

        if (installation is null)
        {
            return NotFound(new
            {
                success = false,
                code = "installation_not_found",
                message = "The requested installation was not found."
            });
        }

        if (installation.OwnerUserId != userId.Value)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    success = false,
                    code = "installation_not_owned",
                    message = "This installation is not linked to the current player."
                });
        }

        installation.OwnerUserId = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Player unlinked device. UserId={UserId}, InstallationId={InstallationId}.",
            userId.Value,
            model.InstallationId);

        return Json(new
        {
            success = true,
            message = "The device was removed from the player fleet.",
            installationId = model.InstallationId
        });
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

        if (!TryParseInstallationId(
            installationId,
            out var parsedInstallationId))
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] =
                "Installation ID format is invalid.";
            return RedirectToAction(nameof(Index));
        }

        var request = new ClaimInstallationRequestModel
        {
            InstallationId = parsedInstallationId,
            UserId = userId.Value
        };

        var result = await _profileApiClient.ClaimInstallationAsync(
            request,
            cancellationToken);

        TempData["ClaimStatusType"] = result?.Success == true
            ? "success"
            : "error";

        TempData["ClaimStatusMessage"] = result?.Message ??
            "The platform could not link this installation right now.";

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

        if (!TryParseInstallationId(
            installationId,
            out var parsedInstallationId))
        {
            TempData["ClaimStatusType"] = "error";
            TempData["ClaimStatusMessage"] =
                "Installation ID format is invalid.";
            return RedirectToAction(nameof(Index));
        }

        var request = new UnlinkInstallationRequestModel
        {
            InstallationId = parsedInstallationId,
            UserId = userId.Value
        };

        var result = await _profileApiClient.UnlinkInstallationAsync(
            request,
            cancellationToken);

        TempData["ClaimStatusType"] = result?.Success == true
            ? "success"
            : "error";

        TempData["ClaimStatusMessage"] = result?.Message ??
            "The platform could not unlink this installation right now.";

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
            TempData["ProfileStatusMessage"] =
                "Check the profile fields and try again.";
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
            TempData["ProfileStatusMessage"] = result?.Message ??
                "The platform could not update your profile.";
            return RedirectToAction(nameof(Index));
        }

        var refreshedUser = await _userManager.FindByIdAsync(
            userId.Value.ToString());

        if (refreshedUser is null)
        {
            TempData["ProfileStatusType"] = "error";
            TempData["ProfileStatusMessage"] =
                "Profile updated, but the current session could not be refreshed.";
            return RedirectToAction(nameof(Index));
        }

        await _userSessionService.RefreshCurrentPrincipalAsync(
            HttpContext,
            refreshedUser,
            cancellationToken);

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
                Items = result.Items.Select(x =>
                    new ProfileSessionHistoryRowViewModel
                    {
                        SessionId = x.SessionId,
                        InstallationId = x.InstallationId,
                        DeviceName = string.IsNullOrWhiteSpace(x.DeviceName)
                            ? "-"
                            : x.DeviceName,
                        BuildVersion = string.IsNullOrWhiteSpace(x.BuildVersion)
                            ? "-"
                            : x.BuildVersion,
                        CurrentScene = string.IsNullOrWhiteSpace(x.CurrentScene)
                            ? "-"
                            : x.CurrentScene,
                        CurrentPhase = string.IsNullOrWhiteSpace(x.CurrentPhase)
                            ? "-"
                            : x.CurrentPhase,
                        StatusLabel = string.IsNullOrWhiteSpace(x.StatusLabel)
                            ? "-"
                            : x.StatusLabel,
                        IsLive = x.IsLive,
                        StartedAtLabel = FormatDateTime(x.StartedAtUtc),
                        DurationLabel = FormatMinutes(x.DurationMinutes),
                        LastHeartbeatLabel = FormatRelativeDate(
                            x.LastHeartbeatUtc)
                    })
                    .ToList()
            };

        ViewData["Title"] = "SESSION HISTORY";
        ViewData["TitleResourceKey"] = "Profile_SessionsPageTitle";

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
            DeviceName = string.IsNullOrWhiteSpace(detail.DeviceName)
                ? "-"
                : detail.DeviceName,
            DeviceModel = string.IsNullOrWhiteSpace(detail.DeviceModel)
                ? "-"
                : detail.DeviceModel,
            Platform = string.IsNullOrWhiteSpace(detail.Platform)
                ? "-"
                : detail.Platform,
            BuildVersion = string.IsNullOrWhiteSpace(detail.BuildVersion)
                ? "-"
                : detail.BuildVersion,
            CurrentScene = string.IsNullOrWhiteSpace(detail.CurrentScene)
                ? "-"
                : detail.CurrentScene,
            CurrentGameState = string.IsNullOrWhiteSpace(
                detail.CurrentGameState)
                    ? "-"
                    : detail.CurrentGameState,
            CurrentPhase = string.IsNullOrWhiteSpace(detail.CurrentPhase)
                ? "-"
                : detail.CurrentPhase,
            StatusLabel = string.IsNullOrWhiteSpace(detail.StatusLabel)
                ? "-"
                : detail.StatusLabel,
            IsLive = detail.IsLive,
            StartedAtLabel = FormatDateTime(detail.StartedAtUtc),
            EndedAtLabel = detail.EndedAtUtc.HasValue
                ? FormatDateTime(detail.EndedAtUtc.Value)
                : "Not ended",
            LastHeartbeatLabel = FormatDateTime(detail.LastHeartbeatUtc),
            DurationLabel = FormatMinutes(detail.DurationMinutes),
            Events = detail.Events.Select(x =>
                new ProfileSessionEventTimelineItemViewModel
                {
                    Id = x.Id,
                    EventType = string.IsNullOrWhiteSpace(x.EventType)
                        ? "-"
                        : x.EventType,
                    Scene = string.IsNullOrWhiteSpace(x.Scene)
                        ? "-"
                        : x.Scene,
                    GameState = string.IsNullOrWhiteSpace(x.GameState)
                        ? "-"
                        : x.GameState,
                    Phase = string.IsNullOrWhiteSpace(x.Phase)
                        ? "-"
                        : x.Phase,
                    PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson)
                        ? string.Empty
                        : x.PayloadJson,
                    HasPayload = !string.IsNullOrWhiteSpace(x.PayloadJson),
                    ClientTimeLabel = x.ClientTimeUtc.HasValue
                        ? FormatDateTime(x.ClientTimeUtc.Value)
                        : "Not provided",
                    CreatedAtLabel = FormatDateTime(x.CreatedAtUtc)
                })
                .ToList()
        };

        ViewData["Title"] = "SESSION DETAIL";
        ViewData["TitleResourceKey"] =
            "Profile_SessionDetailPageTitle";

        return View("SessionDetail", model);
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdClaim, out var userId)
            ? userId
            : null;
    }

    private object CreateValidationResponse()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors
                    .Select(error => error.ErrorMessage)
                    .ToArray());

        return new
        {
            success = false,
            code = "validation_failed",
            message = "One or more fields are invalid.",
            errors
        };
    }

    private static string NormalizeSection(string? section)
    {
        return section?.Trim().ToLowerInvariant() switch
        {
            "security" => "security",
            "fleet" => "fleet",
            _ => "identity"
        };
    }

    private static bool TryParseInstallationId(
        string? installationId,
        out Guid parsedInstallationId)
    {
        parsedInstallationId = Guid.Empty;

        return !string.IsNullOrWhiteSpace(installationId) &&
            Guid.TryParse(
                installationId.Trim(),
                out parsedInstallationId);
    }

    private static string FormatMinutes(long totalMinutes)
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

    private sealed class PlayerSessionSummaryRow
    {
        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset? EndedAtUtc { get; set; }

        public DateTimeOffset LastHeartbeatUtc { get; set; }

        public string BuildVersion { get; set; } = string.Empty;
    }

    private sealed class ActiveFleetSessionRow
    {
        public int InstallationDbId { get; set; }

        public string CurrentScene { get; set; } = string.Empty;

        public DateTimeOffset LastHeartbeatUtc { get; set; }
    }
}
