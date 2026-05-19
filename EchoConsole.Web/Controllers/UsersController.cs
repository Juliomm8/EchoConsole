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
        pageNumber = pageNumber < 1 ? 1 : pageNumber;

        try
        {
            var response = await _usersApiClient.GetUsersAsync(
                searchTerm,
                pageNumber,
                DefaultPageSize,
                cancellationToken);

            var model = new UsersIndexViewModel
            {
                SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
                PageNumber = response.PageNumber > 0 ? response.PageNumber : pageNumber,
                PageSize = response.PageSize > 0 ? response.PageSize : DefaultPageSize,
                TotalCount = response.TotalCount,
                TotalPages = response.TotalPages > 0 ? response.TotalPages : 1,
                Items = response.Items ?? Array.Empty<UserApiDto>()
            };

            ViewData["Title"] = "USER MANAGEMENT";
            ViewData["TitleI18nKey"] = "users_page_title";

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users page.");

            var fallbackModel = new UsersIndexViewModel
            {
                SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
                PageNumber = pageNumber,
                PageSize = DefaultPageSize,
                TotalCount = 0,
                TotalPages = 1,
                Items = Array.Empty<UserApiDto>()
            };

            ViewData["Title"] = "USER MANAGEMENT";
            ViewData["TitleI18nKey"] = "users_page_title";

            return View(fallbackModel);
        }
    }
}