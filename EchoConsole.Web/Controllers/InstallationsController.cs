using EchoConsole.Web.Models.Installations;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
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
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            var response = await _installationsApiClient
                .GetInstallationsAsync(
                    searchTerm,
                    pageNumber,
                    pageSize,
                    cancellationToken);

            SetPageMetadata();

            return View(new InstallationsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = response.Page > 0
                    ? response.Page
                    : pageNumber,
                PageSize = response.PageSize > 0
                    ? response.PageSize
                    : pageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0
                    ? response.TotalPages
                    : 1,
                Items = response.Items
                    .Select(MapRow)
                    .ToArray()
            });
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
                "Failed to build installations inventory page.");

            SetPageMetadata();

            return View(new InstallationsIndexViewModel
            {
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = 1
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListData(
        string? searchTerm,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            return Ok(
                await _installationsApiClient.GetInstallationsAsync(
                    searchTerm,
                    pageNumber,
                    pageSize,
                    cancellationToken));
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
                "Failed to proxy installations data from API.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "Installations data is temporarily unavailable."
                });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMetadata(
        UpdateInstallationMetadataViewModel input,
        string? searchTerm,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["InstallationsError"] =
                "Installations_UpdateValidationError";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    searchTerm,
                    pageNumber,
                    pageSize
                });
        }

        var updated =
            await _installationsApiClient.UpdateMetadataAsync(
                input.InstallationId,
                new UpdateInstallationMetadataApiRequest
                {
                    AdminAlias = input.AdminAlias,
                    AdminStatus = input.AdminStatus
                },
                cancellationToken);

        TempData[
            updated
                ? "InstallationsSuccess"
                : "InstallationsError"] =
            updated
                ? "Installations_UpdateSuccess"
                : "Installations_UpdateError";

        return RedirectToAction(
            nameof(Index),
            new
            {
                searchTerm,
                pageNumber,
                pageSize
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid installationId,
        string? searchTerm,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _installationsApiClient.DeleteAsync(
            installationId,
            cancellationToken);

        TempData[
            deleted
                ? "InstallationsSuccess"
                : "InstallationsError"] =
            deleted
                ? "Installations_DeleteSuccess"
                : "Installations_DeleteError";

        return RedirectToAction(
            nameof(Index),
            new
            {
                searchTerm,
                pageNumber,
                pageSize
            });
    }

    private static InstallationRowViewModel MapRow(
        InstallationListItemApiDto source)
    {
        return new InstallationRowViewModel
        {
            InstallationId =
                source.InstallationId
                    .ToString()
                    .ToUpperInvariant(),
            GameCode = Normalize(source.GameCode),
            BuildVersion = Normalize(source.BuildVersion),
            Platform = Normalize(source.Platform),
            DeviceName = Normalize(source.DeviceName),
            DeviceModel = Normalize(source.DeviceModel),
            OSVersion = Normalize(source.OSVersion),
            Processor = Normalize(source.Processor),
            Gpu = Normalize(source.Gpu),
            RamLabel = source.RamMb.HasValue
                ? $"{source.RamMb.Value:N0} MB"
                : "-",
            TelemetryStatus = Normalize(source.TelemetryStatus),
            AdminAlias = Normalize(source.AdminAlias),
            AdminStatus = Normalize(source.AdminStatus),
            OwnerLabel = source.OwnerUserId.HasValue
                ? $"{Normalize(source.OwnerAlias)} / #{source.OwnerUserId.Value}"
                : "-",
            FirstSeenUtcLabel = FormatUtc(source.FirstSeenUtc),
            LastUpdateUtcLabel = FormatUtc(source.LastUpdateUtc)
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value == default
            ? "-"
            : value.UtcDateTime.ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "DEVICES & INSTALLATIONS";
        ViewData["TitleResourceKey"] =
            "Installations_PageTitle";
    }
}
