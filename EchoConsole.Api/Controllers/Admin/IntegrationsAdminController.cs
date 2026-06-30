using EchoConsole.Api.Configuration;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EchoConsole.Api.Controllers.Admin;

[Authorize(Policy = AdminApiKeyAuthenticationOptions.AdminPolicy)]
[ApiController]
[Route("api/admin/integrations")]
public sealed class IntegrationsAdminController : ControllerBase
{
    private readonly IOptionsMonitor<DiscordOptions> _discordOptions;

    public IntegrationsAdminController(
        IOptionsMonitor<DiscordOptions> discordOptions)
    {
        _discordOptions = discordOptions;
    }

    [HttpGet("discord")]
    public IActionResult Discord()
    {
        var options = _discordOptions.CurrentValue;

        var configured =
            Uri.TryCreate(
                options.WebhookUrl?.Trim(),
                UriKind.Absolute,
                out var uri) &&
            string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            enabled = options.Enabled,
            configured,
            operational = options.Enabled && configured,
            checkedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
