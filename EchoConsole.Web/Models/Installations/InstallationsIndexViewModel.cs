namespace EchoConsole.Web.Models.Installations;

public sealed class InstallationsIndexViewModel
{
    public string SearchTerm { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public IReadOnlyList<InstallationRowViewModel> Items { get; set; } = Array.Empty<InstallationRowViewModel>();
}