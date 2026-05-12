namespace EchoConsole.Api.Contracts.Admin;

public sealed class PagedGameBuildsResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<GameBuildListItemDto> Items { get; set; } = Array.Empty<GameBuildListItemDto>();
}