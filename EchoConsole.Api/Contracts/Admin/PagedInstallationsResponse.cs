namespace EchoConsole.Api.Contracts.Admin;

public sealed class PagedInstallationsResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<InstallationListItemDto> Items { get; set; } = Array.Empty<InstallationListItemDto>();
}