using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EchoConsole.Web.Security;

public sealed class EchoConsoleUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
{
    public EchoConsoleUserClaimsPrincipalFactory(
        UserManager<User> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        identity.AddClaim(new Claim("alias", user.Alias ?? user.Name ?? user.Email ?? "User"));
        identity.AddClaim(new Claim("theme", user.Theme ?? "cyan"));
        identity.AddClaim(new Claim("avatar", user.AvatarKey ?? "avatar-01"));
        identity.AddClaim(new Claim("app_status", user.Status.ToString()));

        return identity;
    }
}