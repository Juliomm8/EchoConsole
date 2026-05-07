namespace EchoConsole.Api.Contracts.Dashboard;

public sealed class DashboardOverviewDto
{
    public int ActiveSessions { get; set; }

    public int RegisteredInstallations { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; }
}