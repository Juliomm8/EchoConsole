using EchoConsole.Web.Models.Api.LiveOperations;
using EchoConsole.Web.Models.LiveOperations;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/LiveOperations")]
public sealed class LiveOperationsController : Controller
{
    private readonly EchoConsoleLiveOperationsApiClient _apiClient;
    private readonly ILogger<LiveOperationsController> _logger;

    public LiveOperationsController(
        EchoConsoleLiveOperationsApiClient apiClient,
        ILogger<LiveOperationsController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var snapshot = await _apiClient.GetSnapshotAsync(
            cancellationToken);

        ViewData["Title"] = "LIVE OPERATIONS";

        return View(new LiveOperationsIndexViewModel
        {
            IsAvailable = snapshot is not null,

            Snapshot = snapshot ?? new LiveOperationsSnapshotApiModel
            {
                ServerTimeUtc = DateTimeOffset.UtcNow
            }
        });
    }

    [HttpGet("Snapshot")]
    public async Task<IActionResult> Snapshot(
        CancellationToken cancellationToken)
    {
        var snapshot = await _apiClient.GetSnapshotAsync(
            cancellationToken);

        if (snapshot is null)
        {
            _logger.LogWarning(
                "Live operations BFF snapshot was unavailable.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Live operations snapshot is unavailable."
                });
        }

        return Ok(snapshot);
    }
}