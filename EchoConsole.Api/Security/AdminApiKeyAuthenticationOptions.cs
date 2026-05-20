using Microsoft.AspNetCore.Authentication;

namespace EchoConsole.Api.Security;

public sealed class AdminApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "AdminApiKey";
    public const string AdminPolicy = "AdminApiPolicy";
    public const string HeaderName = "X-Admin-Api-Key";

    public string ApiKey { get; set; } = string.Empty;
}