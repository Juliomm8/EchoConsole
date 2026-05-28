namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileSessionHistoryViewModel
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }

    public IReadOnlyList<ProfileSessionHistoryRowViewModel> Items { get; set; } = Array.Empty<ProfileSessionHistoryRowViewModel>();
}