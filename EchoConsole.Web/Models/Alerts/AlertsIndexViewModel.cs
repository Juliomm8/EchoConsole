using EchoConsole.Web.Services.Api;

namespace EchoConsole.Web.Models.Alerts;

public sealed class AlertsIndexViewModel
{
    public string? SeverityFilter { get; set; }
    public string StatusFilter { get; set; } = "OPEN";
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public IReadOnlyList<SystemAlertApiDto> Items { get; set; } =
        Array.Empty<SystemAlertApiDto>();
    public IReadOnlyList<AlertTypeDefinitionApiDto> AlertTypes { get; set; } =
        Array.Empty<AlertTypeDefinitionApiDto>();
}
