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
        pageNumber = pageNumber < 1 ? 1 : pageNumber;

        try
        {
            var response = await _alertsApiClient.GetAlertsAsync(
                severity,
                isResolved,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            var model = new AlertsIndexViewModel
            {
                SeverityFilter = string.IsNullOrWhiteSpace(severity) ? null : severity.Trim(),
                IsResolvedFilter = isResolved,
                PageNumber = response.PageNumber > 0 ? response.PageNumber : pageNumber,
                PageSize = response.PageSize > 0 ? response.PageSize : DefaultPageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0 ? response.TotalPages : 1,
                Items = response.Items ?? Array.Empty<SystemAlertApiDto>()
            };

            ViewData["Title"] = "ALERTS AND REPORTS";
            ViewData["TitleI18nKey"] = "alerts_page_title";

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load alerts page.");

            var fallbackModel = new AlertsIndexViewModel
            {
                SeverityFilter = string.IsNullOrWhiteSpace(severity) ? null : severity.Trim(),
                IsResolvedFilter = isResolved,
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalCount = 0,
                TotalPages = 1,
                Items = Array.Empty<SystemAlertApiDto>()
            };

            ViewData["Title"] = "ALERTS AND REPORTS";
            ViewData["TitleI18nKey"] = "alerts_page_title";

            return View(fallbackModel);
        }
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
        try
        {
            await _alertsApiClient.ResolveAlertAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve alert {AlertId}.", id);
        }

        return RedirectToAction(nameof(Index), new
        {
            severity,
            isResolved,
            pageNumber
        });
    }
}