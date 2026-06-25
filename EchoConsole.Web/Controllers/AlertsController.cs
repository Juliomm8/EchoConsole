using System.Globalization;
using EchoConsole.Web.Models.Alerts;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AlertsController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly EchoConsoleAlertsApiClient _alertsApiClient;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        EchoConsoleAlertsApiClient alertsApiClient,
        ILogger<AlertsController> logger)
    {
        _alertsApiClient = alertsApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? severity = null,
        string status = "OPEN",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var model = await LoadModelAsync(
            severity,
            NormalizeStatus(status),
            Math.Max(1, pageNumber),
            cancellationToken);

        ViewData["Title"] = "ALERTS AND REPORTS";
        ViewData["TitleResourceKey"] = "Alerts_PageTitle";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RealtimePage(
        string? severity = null,
        string status = "OPEN",
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var response = await _alertsApiClient.GetAlertsAsync(
            severity,
            NormalizeStatus(status),
            Math.Max(1, pageNumber),
            DefaultPageSize,
            cancellationToken);

        return Json(response);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveRealtime(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await _alertsApiClient.ResolveAlertAsync(
            id,
            cancellationToken);

        return result is null
            ? StatusCode(502, new { message = "The alert could not be resolved." })
            : Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAiTrendAnalysis(
        CancellationToken cancellationToken = default)
    {
        var result = await _alertsApiClient.RunAiTrendAnalysisAsync(
            CultureInfo.CurrentUICulture.Name,
            cancellationToken);

        return result is null
            ? StatusCode(502, new { message = "AI trend analysis could not be generated." })
            : Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAlertType(
        int id,
        string name,
        string description,
        string defaultSeverity,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var result = await _alertsApiClient.UpdateAlertTypeAsync(
            id,
            new UpdateAlertTypeApiRequest
            {
                Name = name,
                Description = description,
                DefaultSeverity = defaultSeverity,
                IsActive = isActive
            },
            cancellationToken);

        return result is null
            ? StatusCode(502, new { message = "The alert type could not be updated." })
            : Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAlertType(
        int id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _alertsApiClient.DeleteAlertTypeAsync(
            id,
            cancellationToken);

        return deleted
            ? NoContent()
            : StatusCode(502, new { message = "The alert type could not be deleted." });
    }

    private async Task<AlertsIndexViewModel> LoadModelAsync(
        string? severity,
        string status,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var alertsTask = _alertsApiClient.GetAlertsAsync(
                severity,
                status,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            var typesTask = _alertsApiClient.GetAlertTypesAsync(
                cancellationToken);

            await Task.WhenAll(alertsTask, typesTask);

            var response = await alertsTask;

            return new AlertsIndexViewModel
            {
                SeverityFilter = string.IsNullOrWhiteSpace(severity)
                    ? null
                    : severity.Trim(),
                StatusFilter = status,
                PageNumber = response.PageNumber > 0 ? response.PageNumber : pageNumber,
                PageSize = response.PageSize > 0 ? response.PageSize : DefaultPageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0 ? response.TotalPages : 1,
                Items = response.Items ?? Array.Empty<SystemAlertApiDto>(),
                AlertTypes = await typesTask
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load alerts page.");

            return new AlertsIndexViewModel
            {
                SeverityFilter = severity?.Trim(),
                StatusFilter = status,
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalPages = 1
            };
        }
    }

    private static string NormalizeStatus(string? status)
    {
        return string.Equals(status, "RESOLVED", StringComparison.OrdinalIgnoreCase)
            ? "RESOLVED"
            : "OPEN";
    }
}
