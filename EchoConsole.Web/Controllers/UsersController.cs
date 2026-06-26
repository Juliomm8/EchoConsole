using EchoConsole.Web.Models.Users;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UsersController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly EchoConsoleUsersApiClient _usersApiClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        EchoConsoleUsersApiClient usersApiClient,
        ILogger<UsersController> logger)
    {
        _usersApiClient = usersApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm = null,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        var normalizedSearch = string.IsNullOrWhiteSpace(searchTerm)
            ? null
            : searchTerm.Trim();

        try
        {
            var response = await _usersApiClient.GetUsersAsync(
                normalizedSearch,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            var model = new UsersIndexViewModel
            {
                SearchTerm = normalizedSearch,
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
                AdminCount = response.AdminCount,
                ViewerCount = response.ViewerCount,
                Items = response.Items
            };

            SetPageTitle();
            return View(model);
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
                "Failed to load users operations page.");

            SetPageTitle();

            return View(new UsersIndexViewModel
            {
                SearchTerm = normalizedSearch,
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalPages = 1
            });
        }
    }

    private void SetPageTitle()
    {
        ViewData["Title"] = "USER MANAGEMENT";
        ViewData["TitleResourceKey"] = "Users_PageTitle";
    }
}
