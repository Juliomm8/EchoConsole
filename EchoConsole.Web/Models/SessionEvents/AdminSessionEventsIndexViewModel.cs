namespace EchoConsole.Web.Models.SessionEvents;

public sealed class AdminSessionEventsIndexViewModel
{
    public string EventType { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public IReadOnlyList<string> AvailableEventTypes { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<string> AvailableBuildVersions { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<AdminSessionEventRowViewModel> Items { get; set; } =
        Array.Empty<AdminSessionEventRowViewModel>();
}