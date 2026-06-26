using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace EchoConsole.Web.Security;

public sealed class EchoConsoleCookieAuthenticationEvents
    : CookieAuthenticationEvents
{
    private readonly ISecurityStampValidator _securityStampValidator;
    private readonly IUserSessionService _userSessionService;
    private readonly ILogger<EchoConsoleCookieAuthenticationEvents> _logger;

    public EchoConsoleCookieAuthenticationEvents(
        ISecurityStampValidator securityStampValidator,
        IUserSessionService userSessionService,
        ILogger<EchoConsoleCookieAuthenticationEvents> logger)
    {
        _securityStampValidator = securityStampValidator;
        _userSessionService = userSessionService;
        _logger = logger;
    }

    public override async Task ValidatePrincipal(
        CookieValidatePrincipalContext context)
    {
        await _securityStampValidator.ValidateAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var isValid = await _userSessionService.ValidatePrincipalAsync(
            context,
            context.HttpContext.RequestAborted);

        if (isValid)
        {
            return;
        }

        _logger.LogWarning(
            "Rejected an authentication cookie because its tracked session is missing, expired or revoked.");

        context.RejectPrincipal();

        await context.HttpContext.SignOutAsync(
            IdentityConstants.ApplicationScheme);
    }
}
