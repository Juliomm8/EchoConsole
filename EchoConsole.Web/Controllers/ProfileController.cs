using System.Security.Claims;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Web;
using EchoConsole.Web.Models.Api.Profile;
using EchoConsole.Web.Models.Profile;
using EchoConsole.Web.Services.Api;
using EchoConsole.Web.Services.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace EchoConsole.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private static readonly TimeSpan ActiveHeartbeatWindow =
        TimeSpan.FromSeconds(90);

    private static readonly TimeSpan DegradedHeartbeatWindow =
        TimeSpan.FromMinutes(5);

    private readonly EchoConsoleDbContext _dbContext;
    private readonly EchoConsoleProfileApiClient _profileApiClient;
    private readonly TimeProvider _timeProvider;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProfileController(
        EchoConsoleDbContext dbContext,
        EchoConsoleProfileApiClient profileApiClient,
        TimeProvider timeProvider,
        IStringLocalizer<SharedResource> localizer)
    {
        _dbContext = dbContext;
        _profileApiClient = profileApiClient;
        _timeProvider = timeProvider;
        _localizer = localizer;
    }

    [HttpGet]
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
                x.AvatarKey,
                x.Role,
                x.Status,
                x.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Challenge();
        }

        var model = new ProfileDashboardViewModel
        {
            UserId = user.Id,
            Alias = user.Alias,
            Name = user.Name,
            AvatarKey = ProfileCatalog.NormalizeStoredAvatarKey(
                user.AvatarKey),
            RoleDisplayName =
                _localizer[GetRoleResourceKey(user.Role)],
            Status =
                _localizer[GetStatusResourceKey(user.Status)],
            CreatedAtUtc = user.CreatedAtUtc
        };

        ViewData["Title"] =
            _localizer["Profile_DashboardPageTitle"].Value;

        ViewData["TitleResourceKey"] =
            "Profile_DashboardPageTitle";

        return View(model);
    }

    [HttpGet("Profile/Api/Dashboard/Live")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public async Task<IActionResult> GetLiveSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var installationIds = await _dbContext.Installations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        if (installationIds.Count == 0)
        {
            return Json(new
            {
                success = true,
                data = new ProfileLiveSnapshotViewModel
                {
                    ConnectionStatus = "Offline",
                    ServerTimeUtc = now
                }
            });
        }

        var sessionQuery = _dbContext.GameSessions
            .AsNoTracking()
            .Where(x =>
                installationIds.Contains(
                    x.InstallationDbId));

        var activeCutoff =
            now.Subtract(ActiveHeartbeatWindow);

        var activeSession = await sessionQuery
            .Where(x =>
                x.Status == SessionStatus.Active &&
                !x.EndedAtUtc.HasValue &&
                x.LastHeartbeatUtc >= activeCutoff)
            .OrderByDescending(x => x.LastHeartbeatUtc)
            .Select(x => new
            {
                x.SessionId,
                x.StartedAtUtc,
                x.LastHeartbeatUtc,
                x.CurrentScene,
                x.CurrentGameState,
                x.CurrentPhase
            })
            .FirstOrDefaultAsync(cancellationToken);

        var lastActivityUtc = await sessionQuery
            .Select(x =>
                (DateTimeOffset?)x.LastHeartbeatUtc)
            .MaxAsync(cancellationToken);

        var connectionStatus = activeSession is not null
            ? "Online"
            : lastActivityUtc.HasValue &&
              lastActivityUtc.Value >=
              now.Subtract(DegradedHeartbeatWindow)
                ? "Degraded"
                : "Offline";

        var snapshot = new ProfileLiveSnapshotViewModel
        {
            ConnectionStatus = connectionStatus,
            HasActiveSession = activeSession is not null,
            ActiveSessionId = activeSession?.SessionId,
            CurrentScene = NormalizeTelemetryValue(
                activeSession?.CurrentScene),
            CurrentGameState = NormalizeTelemetryValue(
                activeSession?.CurrentGameState),
            CurrentPhase = NormalizeTelemetryValue(
                activeSession?.CurrentPhase),
            SessionStartedAtUtc =
                activeSession?.StartedAtUtc,
            LastHeartbeatUtc =
                activeSession?.LastHeartbeatUtc ??
                lastActivityUtc,
            ServerTimeUtc = now
        };

        return Json(new
        {
            success = true,
            data = snapshot
        });
    }

    [HttpGet("Profile/Api/Dashboard/Statistics")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public async Task<IActionResult> GetStatisticsSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var installationIds = await _dbContext.Installations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (installationIds.Count == 0)
        {
            return Json(new
            {
                success = true,
                data = new ProfileStatisticsSnapshotViewModel()
            });
        }

        var sessionQuery = _dbContext.GameSessions
            .AsNoTracking()
            .Where(x =>
                installationIds.Contains(
                    x.InstallationDbId));

        var totalSessions =
            await sessionQuery.CountAsync(
                cancellationToken);

        var totalPlayTimeMinutes = await sessionQuery
            .Select(x =>
                (long?)EF.Functions.DateDiffMinute(
                    x.StartedAtUtc,
                    x.EndedAtUtc ??
                    x.LastHeartbeatUtc))
            .SumAsync(cancellationToken) ?? 0;

        var longestSessionMinutes = await sessionQuery
            .Select(x =>
                (int?)EF.Functions.DateDiffMinute(
                    x.StartedAtUtc,
                    x.EndedAtUtc ??
                    x.LastHeartbeatUtc))
            .MaxAsync(cancellationToken) ?? 0;

        var lastActivityUtc = await sessionQuery
            .Select(x =>
                (DateTimeOffset?)x.LastHeartbeatUtc)
            .MaxAsync(cancellationToken);

        var favoriteBuild = await sessionQuery
            .Where(x =>
                !string.IsNullOrWhiteSpace(
                    x.BuildVersion))
            .GroupBy(x => x.BuildVersion)
            .Select(group => new
            {
                BuildVersion = group.Key,
                SessionCount = group.Count()
            })
            .OrderByDescending(x => x.SessionCount)
            .ThenBy(x => x.BuildVersion)
            .Select(x => x.BuildVersion)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "N/A";

        var snapshot =
            new ProfileStatisticsSnapshotViewModel
            {
                LinkedNodeCount =
                    installationIds.Count,
                TotalSessions =
                    totalSessions,
                TotalPlayTimeMinutes =
                    Math.Max(
                        0,
                        totalPlayTimeMinutes),
                LongestSessionMinutes =
                    Math.Max(
                        0,
                        longestSessionMinutes),
                FavoriteBuild =
                    favoriteBuild,
                LastActivityUtc =
                    lastActivityUtc
            };

        return Json(new
        {
            success = true,
            data = snapshot
        });
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

        return int.TryParse(
            userIdClaim,
            out var userId)
                ? userId
                : null;
    }

    private static string FormatMinutes(
        long totalMinutes)
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

    private static string FormatRelativeDate(
        DateTimeOffset utcDate)
    {
        var diff =
            DateTimeOffset.UtcNow - utcDate;

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

        return utcDate
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm");
    }

    private static string FormatDateTime(
        DateTimeOffset utcDate)
    {
        return utcDate
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm");
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

    private static string NormalizeTelemetryValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "N/A"
            : value.Trim();
    }

}
