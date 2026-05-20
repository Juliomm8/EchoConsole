using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EchoConsole.Api.Security;

public sealed class AdminApiKeyAuthenticationHandler
    : AuthenticationHandler<AdminApiKeyAuthenticationOptions>
{
    public AdminApiKeyAuthenticationHandler(
        IOptionsMonitor<AdminApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AdminApiKeyAuthenticationOptions.HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing admin API key."));
        }

        var providedApiKey = values.ToString();
        var expectedApiKey = Options.ApiKey;

        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Admin API key is not configured on the server."));
        }

        if (!SecureEquals(providedApiKey, expectedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "EchoConsole.Web"),
            new Claim(ClaimTypes.Role, "ServerAdmin"),
            new Claim("api_access", "admin")
        };

        var identity = new ClaimsIdentity(claims, AdminApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AdminApiKeyAuthenticationOptions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        return Response.WriteAsJsonAsync(new
        {
            message = "Missing or invalid admin API key."
        });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";

        return Response.WriteAsJsonAsync(new
        {
            message = "You do not have permission to access this administrative API."
        });
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}