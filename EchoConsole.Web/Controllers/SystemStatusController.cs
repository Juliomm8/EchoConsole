using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/system-status")]
public sealed class SystemStatusController : Controller
{
    private readonly HttpClient _httpClient;

    public SystemStatusController(
        IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(
            EchoConsoleApiClientNames.Admin);
    }

    [HttpGet("discord")]
    public async Task<IActionResult> Discord(
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            "/api/admin/integrations/discord",
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString()
                ?? "application/json",
            Content = string.IsNullOrWhiteSpace(content)
                ? "{}"
                : content
        };
    }
}
