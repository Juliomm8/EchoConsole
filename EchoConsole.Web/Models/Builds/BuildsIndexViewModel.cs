using EchoConsole.Web.Services.Api;

namespace EchoConsole.Web.Models.Builds;

public sealed class BuildsIndexViewModel
{
    public string SearchTerm { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public int TotalRegisteredBuilds { get; set; }

    public string ActiveBuildVersion { get; set; } = string.Empty;

    public string BaseEngineVersion { get; set; } = string.Empty;

    public CreateBuildInputModel NewBuild { get; set; } = new();

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public IReadOnlyList<GameBuildApiDto> Items { get; set; } = Array.Empty<GameBuildApiDto>();
}
