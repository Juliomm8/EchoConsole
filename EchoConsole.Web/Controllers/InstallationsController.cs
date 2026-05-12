using EchoConsole.Web.Models.Installations;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

public sealed class InstallationsController : Controller
{
    private readonly EchoConsoleInstallationsApiClient _installationsApiClient;
    private readonly ILogger<InstallationsController> _logger;

    public InstallationsController(
        EchoConsoleInstallationsApiClient installationsApiClient,
        ILogger<InstallationsController> logger)
    {
        _installationsApiClient = installationsApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        try
        {
            var response = await _installationsApiClient.GetInstallationsAsync(
                searchTerm,
                pageNumber,
                pageSize,
                cancellationToken);

            var model = new InstallationsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = response.Page > 0 ? response.Page : pageNumber,
                PageSize = response.PageSize > 0 ? response.PageSize : pageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0 ? response.TotalPages : 1,
                Items = response.Items.Select(x => new InstallationRowViewModel
                {
                    InstallationId = x.InstallationId.ToString().ToUpperInvariant(),
                    DeviceName = string.IsNullOrWhiteSpace(x.DeviceName) ? "-" : x.DeviceName,
                    OSVersion = string.IsNullOrWhiteSpace(x.OSVersion) ? "-" : x.OSVersion,
                    Processor = string.IsNullOrWhiteSpace(x.Processor) ? "-" : x.Processor!,
                    Gpu = string.IsNullOrWhiteSpace(x.Gpu) ? "-" : x.Gpu!,
                    RamLabel = x.RamMb.HasValue ? $"{x.RamMb.Value:N0} MB" : "-",
                    LastUpdateUtcLabel = x.LastUpdateUtc == default
                        ? "-"
                        : x.LastUpdateUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                }).ToList()
            };

            ViewData["Title"] = "DEVICES & INSTALLATIONS";
            ViewData["TitleI18nKey"] = "installations_page_title";

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build installations inventory page.");

            var fallbackModel = new InstallationsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 1,
                Items = Array.Empty<InstallationRowViewModel>()
            };

            ViewData["Title"] = "DEVICES & INSTALLATIONS";
            ViewData["TitleI18nKey"] = "installations_page_title";

            return View(fallbackModel);
        }
    }
}