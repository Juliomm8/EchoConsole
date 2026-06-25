using EchoConsole.Web.Services.Api;

namespace EchoConsole.Web.Models.Users;

public sealed class UsersIndexViewModel
{
    public string? SearchTerm { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public int AdminCount { get; set; }

    public int ViewerCount { get; set; }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public IReadOnlyList<UserApiDto> Items { get; set; } =
        Array.Empty<UserApiDto>();
}
