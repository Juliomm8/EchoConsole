namespace EchoConsole.Web.Models.Api.SessionEvents;

public sealed class AdminSessionEventsPageApiModel
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }

    public IReadOnlyList<string> AvailableEventTypes { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<string> AvailableBuildVersions { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<AdminSessionEventApiModel> Items { get; set; } =
        Array.Empty<AdminSessionEventApiModel>();
}