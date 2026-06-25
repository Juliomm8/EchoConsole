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
        bool? isResolved = null,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);

        var model = await LoadModelAsync(
            severity,
            isResolved,
            pageNumber,
            cancellationToken);

        SetPageTitle();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RealtimePage(
        string? severity = null,
        bool? isResolved = null,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);

        var response = await _alertsApiClient.GetAlertsAsync(
            severity,
            isResolved,
            pageNumber,
            DefaultPageSize,
            cancellationToken);

        return Json(response);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(
        int id,
        string? severity = null,
        bool? isResolved = null,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        await _alertsApiClient.ResolveAlertAsync(
            id,
            cancellationToken);

        return RedirectToAction(nameof(Index), new
        {
            severity,
            isResolved,
            pageNumber = Math.Max(1, pageNumber)
        });
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

        if (result is null)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "The alert could not be resolved."
                });
        }

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAiTrendAnalysis(
        CancellationToken cancellationToken = default)
    {
        var result = await _alertsApiClient.RunAiTrendAnalysisAsync(
            CultureInfo.CurrentUICulture.Name,
            cancellationToken);

        if (result is null)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "AI trend analysis could not be generated."
                });
        }

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BroadcastDiscord(
        CancellationToken cancellationToken = default)
    {
        var result = await _alertsApiClient.BroadcastDiscordAsync(
            cancellationToken);

        if (result is null)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    sent = false,
                    message = "Discord broadcast could not be completed."
                });
        }

        if (!result.Sent)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                result);
        }

        return Json(result);
    }

    private async Task<AlertsIndexViewModel> LoadModelAsync(
        string? severity,
        bool? isResolved,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _alertsApiClient.GetAlertsAsync(
                severity,
                isResolved,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            return new AlertsIndexViewModel
            {
                SeverityFilter = string.IsNullOrWhiteSpace(severity)
                    ? null
                    : severity.Trim(),
                IsResolvedFilter = isResolved,
                PageNumber = response.PageNumber > 0
                    ? response.PageNumber
                    : pageNumber,
                PageSize = response.PageSize > 0
                    ? response.PageSize
                    : DefaultPageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0
                    ? response.TotalPages
                    : 1,
                Items = response.Items
                    ?? Array.Empty<SystemAlertApiDto>()
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load alerts page.");

            return new AlertsIndexViewModel
            {
                SeverityFilter = severity?.Trim(),
                IsResolvedFilter = isResolved,
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalPages = 1,
                Items = Array.Empty<SystemAlertApiDto>()
            };
        }
    }

    private void SetPageTitle()
    {
        ViewData["Title"] = "ALERTS AND REPORTS";
        ViewData["TitleResourceKey"] = "Alerts_PageTitle";
    }
}
