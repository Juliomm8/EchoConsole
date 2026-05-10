namespace EchoConsole.Web.Models.Dashboard;

public sealed class KpiCardViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string DeltaText { get; set; } = string.Empty;

    public bool IsPositiveDelta { get; set; }

    public string Accent { get; set; } = "cyan";

    public string ValueElementId { get; set; } = string.Empty;
}