using System.Globalization;
using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Web;
using EchoConsole.Web.Models.Api.Profile;
using EchoConsole.Web.Models.Profile;
using EchoConsole.Web.Security;
using EchoConsole.Web.Services.Api;
using EchoConsole.Web.Services.Profile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace EchoConsole.Web.Controllers;

[Authorize]
[Route("Profile/Settings")]
public sealed class ProfileSettingsController : Controller
{
    private const string DeleteConfirmationText =
        "DELETE MY PROFILE";

    private static readonly TimeSpan ActiveHeartbeatWindow =
        TimeSpan.FromSeconds(90);

    private readonly EchoConsoleDbContext _dbContext;
    private readonly EchoConsoleProfileApiClient _profileApiClient;
    private readonly UserManager<User> _userManager;
    private readonly IUserSessionService _userSessionService;
    private readonly TimeProvider _timeProvider;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<ProfileSettingsController> _logger;

    public ProfileSettingsController(
        EchoConsoleDbContext dbContext,
        EchoConsoleProfileApiClient profileApiClient,
        UserManager<User> userManager,
        IUserSessionService userSessionService,
        TimeProvider timeProvider,
        IStringLocalizer<SharedResource> localizer,
        ILogger<ProfileSettingsController> logger)
    {
        _dbContext = dbContext;
        _profileApiClient = profileApiClient;
        _userManager = userManager;
        _userSessionService = userSessionService;
        _timeProvider = timeProvider;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
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

        var selectedAvatar =
            ProfileCatalog.NormalizeStoredAvatarKey(
                user.AvatarKey);

        var selectedTheme =
            ProfileCatalog.NormalizeStoredTheme(
                user.Theme);

        var identity = new IdentityTabViewModel
        {
            UserId = user.Id,
            Alias = user.Alias,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            AvatarKey = selectedAvatar,
            Theme = selectedTheme,
            PreferredLanguage =
                ProfileCatalog.IsAllowedLanguage(
                    user.PreferredLanguage)
                    ? user.PreferredLanguage
                    : ProfileCatalog.DefaultLanguage,
            Role = user.Role.ToString(),
            RoleDisplayName =
                _localizer[GetRoleResourceKey(user.Role)],
            Status =
                _localizer[GetStatusResourceKey(user.Status)],
            CreatedAtUtc = user.CreatedAtUtc
        };

        var avatarOptions = ProfileCatalog.AvatarKeys
            .Select((key, index) =>
                new AvatarOptionViewModel
                {
                    Key = key,
                    DisplayName = _localizer[
                        $"Profile_Avatar_{index + 1:D2}"],
                    ShortCode = $"OP-{index + 1:D2}",
                    ImageUrl =
                        $"/images/avatars/{key}.png",
                    IsSelected = string.Equals(
                        key,
                        selectedAvatar,
                        StringComparison.OrdinalIgnoreCase)
                })
            .ToList();

        var themeOptions = ProfileCatalog.ThemeKeys
            .Select(key =>
                new ThemeOptionViewModel
                {
                    Key = key,
                    DisplayName = _localizer[
                        GetThemeResourceKey(key)],
                    IsSelected = string.Equals(
                        key,
                        selectedTheme,
                        StringComparison.OrdinalIgnoreCase)
                })
            .ToList();

        var model = new ProfileSettingsViewModel
        {
            Identity = identity,
            AvatarOptions = avatarOptions,
            ThemeOptions = themeOptions
        };

        ViewData["Title"] =
            _localizer["Profile_SettingsPageTitle"].Value;

        ViewData["TitleResourceKey"] =
            "Profile_SettingsPageTitle";

        return View(
            "~/Views/Profile/Settings.cshtml",
            model);
    }

    [HttpGet("Api/Security/Sessions")]
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
            _userSessionService.GetCurrentSessionKeyHash(
                User);

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
                var descriptor =
                    UserAgentParser.Parse(
                        row.UserAgent);

                return new ActiveUserSessionViewModel
                {
                    Id = row.Id,
                    Browser = descriptor.Browser,
                    OperatingSystem =
                        descriptor.OperatingSystem,
                    DeviceLabel =
                        descriptor.DeviceLabel,
                    MaskedIpAddress =
                        row.MaskedIpAddress,
                    CreatedAtUtc =
                        row.CreatedAtUtc,
                    LastSeenAtUtc =
                        row.LastSeenAtUtc,
                    ExpiresAtUtc =
                        row.ExpiresAtUtc,
                    IsCurrent = string.Equals(
                        row.SessionKeyHash,
                        currentSessionKeyHash,
                        StringComparison.Ordinal)
                };
            })
            .ToList();

        return Json(new
        {
            success = true,
            data = new SecurityTabViewModel
            {
                HasLocalPassword =
                    await _userManager
                        .HasPasswordAsync(user),
                EmailConfirmed =
                    user.EmailConfirmed,
                ActiveSessionCount =
                    sessions.Count,
                ActiveSessions =
                    sessions
            }
        });
    }

    [HttpGet("Api/Fleet")]
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

        var activeSessionRows =
            installationDbIds.Count == 0
                ? new List<ActiveFleetSessionRow>()
                : await _dbContext.GameSessions
                    .AsNoTracking()
                    .Where(x =>
                        installationDbIds.Contains(
                            x.InstallationDbId) &&
                        x.Status ==
                            SessionStatus.Active &&
                        !x.EndedAtUtc.HasValue)
                    .OrderByDescending(
                        x => x.LastHeartbeatUtc)
                    .Select(x =>
                        new ActiveFleetSessionRow
                        {
                            InstallationDbId =
                                x.InstallationDbId,
                            CurrentScene =
                                x.CurrentScene,
                            LastHeartbeatUtc =
                                x.LastHeartbeatUtc
                        })
                    .ToListAsync(
                        cancellationToken);

        var latestSessionByInstallation =
            activeSessionRows
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

                var hasLiveHeartbeat =
                    liveSession is not null &&
                    liveSession.LastHeartbeatUtc >=
                    activeCutoff;

                return new FleetDeviceViewModel
                {
                    InstallationId =
                        installation.InstallationId,
                    DisplayName =
                        string.IsNullOrWhiteSpace(
                            installation.AdminAlias)
                            ? installation.DeviceName
                            : installation.AdminAlias,
                    DeviceName =
                        installation.DeviceName,
                    DeviceModel =
                        installation.DeviceModel,
                    Platform =
                        installation.Platform,
                    OperatingSystem =
                        installation.OSVersion,
                    BuildVersion =
                        installation.BuildVersion,
                    TelemetryStatus =
                        hasLiveHeartbeat
                            ? "Active"
                            : installation.Status,
                    CurrentScene =
                        hasLiveHeartbeat &&
                        !string.IsNullOrWhiteSpace(
                            liveSession!.CurrentScene)
                            ? liveSession.CurrentScene
                            : "N/A",
                    FirstSeenUtc =
                        installation.FirstSeenUtc,
                    LastUpdateUtc =
                        installation.LastUpdateUtc
                };
            })
            .ToList();

        return Json(new
        {
            success = true,
            data = new FleetTabViewModel
            {
                LinkedDeviceCount = devices.Count,
                Devices = devices
            }
        });
    }

    [HttpPost("Api/Identity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateIdentityAsync(
        [FromBody] UpdateIdentityViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                CreateValidationResponse());
        }

        var alias = model.Alias.Trim();

        var avatarKey =
            ProfileCatalog.NormalizeAvatarKey(
                model.AvatarKey);

        var theme =
            ProfileCatalog.NormalizeTheme(
                model.Theme);

        var preferredLanguage =
            ProfileCatalog.NormalizeLanguage(
                model.PreferredLanguage);

        if (!ProfileCatalog.IsValidAlias(alias))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_alias",
                message = _localizer[
                    "Profile_Error_InvalidAlias"].Value
            });
        }

        if (!ProfileCatalog.IsAllowedAvatarKey(
            avatarKey))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_avatar",
                message = _localizer[
                    "Profile_Error_InvalidAvatar"].Value
            });
        }

        if (!ProfileCatalog.IsAllowedTheme(theme))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_theme",
                message = _localizer[
                    "Profile_Error_InvalidTheme"].Value
            });
        }

        if (!ProfileCatalog.IsAllowedLanguage(
            preferredLanguage))
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_language",
                message = _localizer[
                    "Profile_Error_InvalidLanguage"].Value
            });
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var normalizedAlias =
            alias.ToUpperInvariant();

        var aliasAlreadyExists =
            await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    candidate =>
                        candidate.Id != user.Id &&
                        candidate.Alias.ToUpper() ==
                        normalizedAlias,
                    cancellationToken);

        if (aliasAlreadyExists)
        {
            return Conflict(new
            {
                success = false,
                code = "alias_already_used",
                message = _localizer[
                    "Profile_Error_AliasUsed"].Value
            });
        }

        var previousLanguage =
            CultureInfo.CurrentUICulture
                .TwoLetterISOLanguageName;

        user.Alias = alias;
        user.AvatarKey = avatarKey;
        user.Theme = theme;
        user.PreferredLanguage =
            preferredLanguage;
        user.ProfileUpdatedAtUtc =
            _timeProvider.GetUtcNow();

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                success = false,
                code = "identity_update_failed",
                message = _localizer[
                    "Profile_Error_UpdateFailed"].Value,
                errors = updateResult.Errors
                    .Select(error =>
                        error.Description)
            });
        }

        PersistCultureCookie(
            preferredLanguage);

        await _userSessionService
            .RefreshCurrentPrincipalAsync(
                HttpContext,
                user,
                cancellationToken);

        var reloadRequired =
            !string.Equals(
                previousLanguage,
                preferredLanguage,
                StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Player settings updated. UserId={UserId}, Avatar={Avatar}, Theme={Theme}, Language={Language}.",
            user.Id,
            avatarKey,
            theme,
            preferredLanguage);

        return Json(new
        {
            success = true,
            message = _localizer[
                "Profile_IdentitySaved"].Value,
            data = new
            {
                user.Alias,
                user.AvatarKey,
                user.Theme,
                user.PreferredLanguage,
                user.ProfileUpdatedAtUtc,
                avatarImageUrl =
                    $"/images/avatars/{user.AvatarKey}.png",
                reloadRequired
            }
        });
    }

    [HttpPost("Api/Security/Password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                CreateValidationResponse());
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
                message = _localizer[
                    "Profile_Error_NoLocalPassword"].Value
            });
        }

        var result =
            await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                success = false,
                code = "password_change_failed",
                message = _localizer[
                    "Profile_Error_PasswordChange"].Value,
                errors = result.Errors
                    .Select(x => x.Description)
            });
        }

        var revokedSessions =
            await _userSessionService
                .RevokeOtherSessionsAndRefreshAsync(
                    HttpContext,
                    user,
                    "PasswordChanged",
                    updateSecurityStamp: false,
                    cancellationToken);

        return Json(new
        {
            success = true,
            message = _localizer[
                "Profile_PasswordChanged"].Value,
            revokedSessions
        });
    }

    [HttpPost("Api/Security/Sessions/Revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessionAsync(
        [FromBody] RevokeUserSessionViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                CreateValidationResponse());
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        await _userSessionService.EnsureCurrentSessionAsync(
            HttpContext,
            user,
            cancellationToken);

        var currentSessionKeyHash =
            _userSessionService.GetCurrentSessionKeyHash(
                User);

        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(
                x =>
                    x.Id == model.SessionId &&
                    x.UserId == user.Id,
                cancellationToken);

        if (session is null)
        {
            return NotFound(new
            {
                success = false,
                code = "session_not_found",
                message = _localizer[
                    "Profile_Error_SessionNotFound"].Value
            });
        }

        if (string.Equals(
            session.SessionKeyHash,
            currentSessionKeyHash,
            StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                success = false,
                code = "current_session",
                message = _localizer[
                    "Profile_Error_CurrentSession"].Value
            });
        }

        if (!session.RevokedAtUtc.HasValue)
        {
            session.RevokedAtUtc =
                _timeProvider.GetUtcNow();

            session.RevokedReason =
                "TerminatedByUser";

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        return Json(new
        {
            success = true,
            message = _localizer[
                "Profile_SessionRevoked"].Value
        });
    }

    [HttpPost("Api/Security/Terminate-Others")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        TerminateAllOtherSessionsAsync(
            CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var revokedSessions =
            await _userSessionService
                .RevokeOtherSessionsAndRefreshAsync(
                    HttpContext,
                    user,
                    "TerminatedByUser",
                    updateSecurityStamp: true,
                    cancellationToken);

        return Json(new
        {
            success = true,
            message = _localizer[
                "Profile_RemoteSessionsTerminated"].Value,
            revokedSessions
        });
    }

    [HttpPost("Api/Fleet/Claim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimDeviceAsync(
        [FromBody] ClaimInstallationViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid ||
            model.InstallationId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_installation_id",
                message = _localizer[
                    "Profile_Error_InvalidInstallation"].Value
            });
        }

        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result =
            await _profileApiClient.ClaimInstallationAsync(
                new ClaimInstallationRequestModel
                {
                    InstallationId =
                        model.InstallationId,
                    UserId = userId.Value
                },
                cancellationToken);

        if (result?.Success != true)
        {
            return BadRequest(new
            {
                success = false,
                code = "claim_failed",
                message = result?.Message ??
                    _localizer[
                        "Profile_Error_ClaimFailed"].Value
            });
        }

        return Json(new
        {
            success = true,
            message = result.Message,
            installationId =
                result.InstallationId
        });
    }

    [HttpPost("Api/Fleet/Unlink")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkDeviceAsync(
        [FromBody] UnlinkDeviceViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid ||
            model.InstallationId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_installation_id",
                message = _localizer[
                    "Profile_Error_InvalidInstallation"].Value
            });
        }

        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var installation =
            await _dbContext.Installations
                .FirstOrDefaultAsync(
                    x =>
                        x.InstallationId ==
                        model.InstallationId,
                    cancellationToken);

        if (installation is null)
        {
            return NotFound(new
            {
                success = false,
                code = "installation_not_found",
                message = _localizer[
                    "Profile_Error_InstallationNotFound"].Value
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
                    message = _localizer[
                        "Profile_Error_InstallationNotOwned"].Value
                });
        }

        installation.OwnerUserId = null;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Json(new
        {
            success = true,
            message = _localizer[
                "Profile_DeviceUnlinked"].Value,
            installationId =
                model.InstallationId
        });
    }

    [HttpPost("Api/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProfileAsync(
        [FromBody] DeleteProfileViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                CreateValidationResponse());
        }

        if (!string.Equals(
            model.ConfirmationText.Trim(),
            DeleteConfirmationText,
            StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                success = false,
                code = "confirmation_mismatch",
                message = _localizer[
                    "Profile_Error_DeletePhrase"].Value
            });
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
                code = "local_password_required",
                message = _localizer[
                    "Profile_Error_DeleteRequiresPassword"].Value
            });
        }

        var passwordValid =
            await _userManager.CheckPasswordAsync(
                user,
                model.Password);

        if (!passwordValid)
        {
            return BadRequest(new
            {
                success = false,
                code = "invalid_password",
                message = _localizer[
                    "Profile_Error_InvalidPassword"].Value
            });
        }

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        await _dbContext.Installations
            .Where(x => x.OwnerUserId == user.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.OwnerUserId,
                        (int?)null),
                cancellationToken);

        var deleteResult =
            await _userManager.DeleteAsync(user);

        if (!deleteResult.Succeeded)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return BadRequest(new
            {
                success = false,
                code = "delete_failed",
                message = _localizer[
                    "Profile_Error_DeleteFailed"].Value,
                errors = deleteResult.Errors
                    .Select(x => x.Description)
            });
        }

        await transaction.CommitAsync(
            cancellationToken);

        await HttpContext.SignOutAsync(
            IdentityConstants.ApplicationScheme);

        await HttpContext.SignOutAsync(
            IdentityConstants.ExternalScheme);

        _logger.LogWarning(
            "Player profile permanently deleted. UserId={UserId}.",
            user.Id);

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action(
                "Index",
                "Home")
        });
    }

    private void PersistCultureCookie(
        string culture)
    {
        var requestCulture =
            new RequestCulture(
                culture,
                culture);

        Response.Cookies.Append(
            CookieRequestCultureProvider
                .DefaultCookieName,
            CookieRequestCultureProvider
                .MakeCookieValue(requestCulture),
            new CookieOptions
            {
                Expires = _timeProvider
                    .GetUtcNow()
                    .AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/"
            });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(
            claim,
            out var userId)
                ? userId
                : null;
    }

    private object CreateValidationResponse()
    {
        var errors = ModelState
            .Where(x =>
                x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors
                    .Select(error =>
                        error.ErrorMessage)
                    .ToArray());

        return new
        {
            success = false,
            code = "validation_failed",
            message = _localizer[
                "Profile_Error_Validation"].Value,
            errors
        };
    }

    private static string GetRoleResourceKey(
        UserRole role)
    {
        return role switch
        {
            UserRole.Admin =>
                "Profile_Role_Admin",
            UserRole.Moderator =>
                "Profile_Role_Supervisor",
            _ =>
                "Profile_Role_Player"
        };
    }

    private static string GetStatusResourceKey(
        UserStatus status)
    {
        return status switch
        {
            UserStatus.Suspended =>
                "Profile_AccountStatus_Suspended",
            _ =>
                "Profile_AccountStatus_Active"
        };
    }

    private static string GetThemeResourceKey(
        string key)
    {
        return key switch
        {
            "amber" =>
                "Profile_Theme_Amber",
            "cyan" =>
                "Profile_Theme_Cyan",
            "monochrome" =>
                "Profile_Theme_Monochrome",
            _ =>
                "Profile_Theme_Green"
        };
    }

    private sealed class ActiveFleetSessionRow
    {
        public int InstallationDbId { get; set; }

        public string CurrentScene { get; set; } =
            string.Empty;

        public DateTimeOffset LastHeartbeatUtc { get; set; }
    }
}
