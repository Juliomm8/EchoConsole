namespace EchoConsole.Web.Models.Dashboard;

public sealed class LiveMonitoringDashboardViewModel
{
    public DateTime ServerTimeUtc { get; set; }

    public bool IsLive { get; set; }

    public IReadOnlyList<KpiCardViewModel> Kpis { get; set; } = Array.Empty<KpiCardViewModel>();

    public IReadOnlyList<LiveSessionRowViewModel> Sessions { get; set; } = Array.Empty<LiveSessionRowViewModel>();
}