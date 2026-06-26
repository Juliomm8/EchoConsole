using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Web.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EchoConsole.Web.Security;

public sealed class EchoConsoleUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<User>
{
    public EchoConsoleUserClaimsPrincipalFactory(
        UserManager<User> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
        User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(
            new Claim(
                ClaimTypes.Role,
                user.Role.ToString()));

        identity.AddClaim(
            new Claim(
                "alias",
                user.Alias ??
                user.Name ??
                user.Email ??
                "User"));

        identity.AddClaim(
            new Claim(
                "theme",
                user.Theme ?? ProfileCatalog.DefaultTheme));

        identity.AddClaim(
            new Claim(
                "avatar",
                user.AvatarKey ?? ProfileCatalog.DefaultAvatarKey));

        identity.AddClaim(
            new Claim(
                "app_status",
                user.Status.ToString()));

        identity.AddClaim(
            new Claim(
                EchoConsoleClaimTypes.PreferredLanguage,
                user.PreferredLanguage ??
                ProfileCatalog.DefaultLanguage));

        return identity;
    }
}
