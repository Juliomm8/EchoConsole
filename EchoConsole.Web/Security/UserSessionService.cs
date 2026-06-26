using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Web.Security;

public sealed class UserSessionService : IUserSessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan SessionTouchInterval = TimeSpan.FromMinutes(2);

    private readonly EchoConsoleDbContext _dbContext;
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        EchoConsoleDbContext dbContext,
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        ILogger<UserSessionService> logger)
    {
        _dbContext = dbContext;
        _signInManager = signInManager;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SignInAsync(
        HttpContext httpContext,
        User user,
        bool isPersistent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);

        var sessionKey = CreateSessionKey();
        var now = _timeProvider.GetUtcNow();
        var expiresAtUtc = now.Add(SessionLifetime);

        var session = CreateSessionEntity(
            user.Id,
            sessionKey,
            httpContext,
            now,
            expiresAtUtc);

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var principal = await _signInManager.CreateUserPrincipalAsync(user);
            var sessionPrincipal = AddOrReplaceSessionClaim(principal, sessionKey);

            var properties = new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = isPersistent,
                IssuedUtc = now,
                ExpiresUtc = expiresAtUtc
            };

            await httpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                sessionPrincipal,
                properties);
        }
        catch
        {
            session.RevokedAtUtc = _timeProvider.GetUtcNow();
            session.RevokedReason = "SignInFailed";
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> ValidatePrincipalAsync(
        CookieValidatePrincipalContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var principal = context.Principal;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        var sessionKey = principal.FindFirstValue(
            EchoConsoleClaimTypes.UserSessionKey);

        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user is null)
            {
                return false;
            }

            var upgradedSessionKey = CreateSessionKey();
            var upgradedSession = CreateSessionEntity(
                user.Id,
                upgradedSessionKey,
                context.HttpContext,
                now,
                now.Add(SessionLifetime));

            _dbContext.UserSessions.Add(upgradedSession);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var upgradedPrincipal = AddOrReplaceSessionClaim(
                principal,
                upgradedSessionKey);

            context.ReplacePrincipal(upgradedPrincipal);
            context.ShouldRenew = true;

            _logger.LogInformation(
                "Upgraded legacy authentication cookie to tracked session. UserId={UserId}, SessionId={SessionId}.",
                user.Id,
                upgradedSession.Id);

            return true;
        }

        var sessionKeyHash = HashSessionKey(sessionKey);

        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                     x.SessionKeyHash == sessionKeyHash,
                cancellationToken);

        if (session is null ||
            session.RevokedAtUtc.HasValue ||
            session.ExpiresAtUtc <= now)
        {
            return false;
        }

        if (now - session.LastSeenAtUtc >= SessionTouchInterval)
        {
            session.LastSeenAtUtc = now;
            session.ExpiresAtUtc = now.Add(SessionLifetime);
            session.MaskedIpAddress = MaskIpAddress(
                context.HttpContext.Connection.RemoteIpAddress);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task EnsureCurrentSessionAsync(
        HttpContext httpContext,
        User user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);

        var sessionKey = httpContext.User.FindFirstValue(
            EchoConsoleClaimTypes.UserSessionKey);

        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var newSessionKey = CreateSessionKey();
        var session = CreateSessionEntity(
            user.Id,
            newSessionKey,
            httpContext,
            now,
            now.Add(SessionLifetime));

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var authenticationResult = await httpContext.AuthenticateAsync(
            IdentityConstants.ApplicationScheme);

        var properties = authenticationResult.Properties ??
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false
            };

        properties.IssuedUtc = now;
        properties.ExpiresUtc = now.Add(SessionLifetime);

        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var sessionPrincipal = AddOrReplaceSessionClaim(
            principal,
            newSessionKey);

        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            sessionPrincipal,
            properties);

        httpContext.User = sessionPrincipal;
    }

    public async Task RefreshCurrentPrincipalAsync(
        HttpContext httpContext,
        User user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);

        await EnsureCurrentSessionAsync(
            httpContext,
            user,
            cancellationToken);

        var sessionKey = httpContext.User.FindFirstValue(
            EchoConsoleClaimTypes.UserSessionKey);

        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new InvalidOperationException(
                "The current authentication session could not be resolved.");
        }

        var now = _timeProvider.GetUtcNow();
        var sessionKeyHash = HashSessionKey(sessionKey);

        var currentSession = await _dbContext.UserSessions
            .FirstOrDefaultAsync(
                x => x.UserId == user.Id &&
                     x.SessionKeyHash == sessionKeyHash,
                cancellationToken);

        if (currentSession is null)
        {
            currentSession = CreateSessionEntity(
                user.Id,
                sessionKey,
                httpContext,
                now,
                now.Add(SessionLifetime));

            _dbContext.UserSessions.Add(currentSession);
        }
        else
        {
            currentSession.LastSeenAtUtc = now;
            currentSession.ExpiresAtUtc = now.Add(SessionLifetime);
            currentSession.RevokedAtUtc = null;
            currentSession.RevokedReason = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var authenticationResult = await httpContext.AuthenticateAsync(
            IdentityConstants.ApplicationScheme);

        var properties = authenticationResult.Properties ??
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false
            };

        properties.IssuedUtc = now;
        properties.ExpiresUtc = now.Add(SessionLifetime);

        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var refreshedPrincipal = AddOrReplaceSessionClaim(
            principal,
            sessionKey);

        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            refreshedPrincipal,
            properties);

        httpContext.User = refreshedPrincipal;
    }

    public async Task<int> RevokeOtherSessionsAndRefreshAsync(
        HttpContext httpContext,
        User user,
        string reason,
        bool updateSecurityStamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);

        await EnsureCurrentSessionAsync(
            httpContext,
            user,
            cancellationToken);

        var currentSessionKeyHash = GetCurrentSessionKeyHash(
            httpContext.User);

        if (string.IsNullOrWhiteSpace(currentSessionKeyHash))
        {
            throw new InvalidOperationException(
                "The current authentication session could not be resolved.");
        }

        var now = _timeProvider.GetUtcNow();
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "RevokedByUser"
            : reason.Trim();

        var revokedCount = await _dbContext.UserSessions
            .Where(x =>
                x.UserId == user.Id &&
                x.SessionKeyHash != currentSessionKeyHash &&
                !x.RevokedAtUtc.HasValue)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevokedReason, normalizedReason),
                cancellationToken);

        if (updateSecurityStamp)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);

            if (!stampResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    stampResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"The security stamp could not be updated: {errors}");
            }
        }

        await RefreshCurrentPrincipalAsync(
            httpContext,
            user,
            cancellationToken);

        _logger.LogInformation(
            "Revoked {SessionCount} authentication sessions for UserId={UserId}. Current session was preserved.",
            revokedCount,
            user.Id);

        return revokedCount;
    }

    public async Task RevokeCurrentSessionAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var userIdValue = httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var sessionKeyHash = GetCurrentSessionKeyHash(
            httpContext.User);

        if (int.TryParse(userIdValue, out var userId) &&
            !string.IsNullOrWhiteSpace(sessionKeyHash))
        {
            var session = await _dbContext.UserSessions
                .FirstOrDefaultAsync(
                    x => x.UserId == userId &&
                         x.SessionKeyHash == sessionKeyHash,
                    cancellationToken);

            if (session is not null && !session.RevokedAtUtc.HasValue)
            {
                session.RevokedAtUtc = _timeProvider.GetUtcNow();
                session.RevokedReason = string.IsNullOrWhiteSpace(reason)
                    ? "SignedOut"
                    : reason.Trim();

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await httpContext.SignOutAsync(
            IdentityConstants.ApplicationScheme);

        await httpContext.SignOutAsync(
            IdentityConstants.ExternalScheme);
    }

    public string? GetCurrentSessionKeyHash(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var sessionKey = principal.FindFirstValue(
            EchoConsoleClaimTypes.UserSessionKey);

        return string.IsNullOrWhiteSpace(sessionKey)
            ? null
            : HashSessionKey(sessionKey);
    }

    private static UserSession CreateSessionEntity(
        int userId,
        string sessionKey,
        HttpContext httpContext,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc)
    {
        var userAgent = httpContext.Request.Headers["User-Agent"].ToString();

        return new UserSession
        {
            UserId = userId,
            SessionKeyHash = HashSessionKey(sessionKey),
            UserAgent = string.IsNullOrWhiteSpace(userAgent)
                ? "Unknown"
                : userAgent[..Math.Min(userAgent.Length, 512)],
            MaskedIpAddress = MaskIpAddress(
                httpContext.Connection.RemoteIpAddress),
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private static ClaimsPrincipal AddOrReplaceSessionClaim(
        ClaimsPrincipal principal,
        string sessionKey)
    {
        var identities = principal.Identities
            .Select(identity => new ClaimsIdentity(identity))
            .ToArray();

        var clonedPrincipal = new ClaimsPrincipal(identities);
        var targetIdentity = clonedPrincipal.Identities
            .FirstOrDefault(identity => identity.IsAuthenticated)
            ?? clonedPrincipal.Identities.First();

        foreach (var existingClaim in targetIdentity
            .FindAll(EchoConsoleClaimTypes.UserSessionKey)
            .ToArray())
        {
            targetIdentity.RemoveClaim(existingClaim);
        }

        targetIdentity.AddClaim(
            new Claim(
                EchoConsoleClaimTypes.UserSessionKey,
                sessionKey));

        return clonedPrincipal;
    }

    private static string CreateSessionKey()
    {
        return WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(32));
    }

    private static string HashSessionKey(string sessionKey)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(sessionKey));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string MaskIpAddress(IPAddress? address)
    {
        if (address is null)
        {
            return "Unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily ==
            System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.xxx";
        }

        if (address.AddressFamily ==
            System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var segments = address
                .ToString()
                .Split(':', StringSplitOptions.RemoveEmptyEntries);

            var visibleSegments = segments.Take(4);
            return $"{string.Join(':', visibleSegments)}::";
        }

        return "Unknown";
    }
}
