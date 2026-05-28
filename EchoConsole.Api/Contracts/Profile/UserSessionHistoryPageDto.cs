namespace EchoConsole.Api.Contracts.Profile;

public sealed class UserSessionHistoryPageDto
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }

    public IReadOnlyList<UserSessionHistoryItemDto> Items { get; set; } = Array.Empty<UserSessionHistoryItemDto>();
}