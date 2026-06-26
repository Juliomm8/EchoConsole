namespace EchoConsole.Api.Contracts.Admin;

public sealed class PagedUsersResponse
{
    public IReadOnlyList<UserListItemDto> Items { get; set; } =
        Array.Empty<UserListItemDto>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public int AdminCount { get; set; }

    public int ViewerCount { get; set; }
}
