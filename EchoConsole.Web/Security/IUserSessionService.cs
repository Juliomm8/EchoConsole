using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EchoConsole.Web.Security;

public interface IUserSessionService
{
    Task SignInAsync(
        HttpContext httpContext,
        User user,
        bool isPersistent,
        CancellationToken cancellationToken = default);

    Task<bool> ValidatePrincipalAsync(
        CookieValidatePrincipalContext context,
        CancellationToken cancellationToken = default);

    Task EnsureCurrentSessionAsync(
        HttpContext httpContext,
        User user,
        CancellationToken cancellationToken = default);

    Task RefreshCurrentPrincipalAsync(
        HttpContext httpContext,
        User user,
        CancellationToken cancellationToken = default);

    Task<int> RevokeOtherSessionsAndRefreshAsync(
        HttpContext httpContext,
        User user,
        string reason,
        bool updateSecurityStamp,
        CancellationToken cancellationToken = default);

    Task RevokeCurrentSessionAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken = default);

    string? GetCurrentSessionKeyHash(ClaimsPrincipal principal);
}
