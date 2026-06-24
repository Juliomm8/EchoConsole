using EchoConsole.Web.Models.Simulation;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/simulation")]
public sealed class SimulationController : Controller
{
    private readonly EchoConsoleSimulationApiClient _apiClient;

    public SimulationController(
        EchoConsoleSimulationApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(
        CancellationToken cancellationToken)
    {
        return ToActionResult(
            await _apiClient.GetStatusAsync(
                cancellationToken));
    }

    [HttpPost("reconcile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reconcile(
        [FromBody] SimulationTargetInput request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return ToActionResult(
            await _apiClient.ReconcileAsync(
                request,
                cancellationToken));
    }

    [HttpPost("pulse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pulse(
        [FromBody] SimulationTargetInput request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return ToActionResult(
            await _apiClient.PulseAsync(
                request,
                cancellationToken));
    }

    [HttpPost("alerts/critical")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InjectCriticalAlert(
        [FromBody] SimulationCommandInput request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return ToActionResult(
            await _apiClient.InjectCriticalAlertAsync(
                request,
                cancellationToken));
    }

    [HttpPost("mass-drop")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MassDrop(
        [FromBody] SimulationCommandInput request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return ToActionResult(
            await _apiClient.MassDropAsync(
                request,
                cancellationToken));
    }

    [HttpPost("purge-simulated")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeSimulatedData(
        [FromBody] SimulationConfirmationInput request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return ToActionResult(
            await _apiClient.PurgeSimulatedDataAsync(
                request,
                cancellationToken));
    }

    [HttpPost("wipe-telemetry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WipeTelemetry(
        [FromBody] SimulationConfirmationInput request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return ToActionResult(
            await _apiClient.WipeTelemetryAsync(
                request,
                cancellationToken));
    }

    private static ContentResult ToActionResult(
        SimulationApiResponse response)
    {
        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Content = response.Content
        };
    }
}
