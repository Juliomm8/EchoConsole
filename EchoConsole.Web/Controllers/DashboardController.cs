using EchoConsole.Web.Models.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

public sealed class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var model = new LiveMonitoringDashboardViewModel
        {
            ServerTimeUtc = DateTime.UtcNow,
            IsLive = true,
            Kpis = new List<KpiCardViewModel>
            {
                new()
                {
                    Title = "Registered Installations",
                    Value = "2",
                    Subtitle = "Validated over real internet ingestion",
                    DeltaText = "+1 new device",
                    IsPositiveDelta = true,
                    Accent = "cyan",
                    ValueElementId = "kpi-registered-installations"
                },
                new()
                {
                    Title = "Active Sessions",
                    Value = "1",
                    Subtitle = "Updated through SignalR",
                    DeltaText = "Live now",
                    IsPositiveDelta = true,
                    Accent = "magenta",
                    ValueElementId = "kpi-active-sessions"
                },
                new()
                {
                    Title = "Average Session Duration",
                    Value = "00:18:42",
                    Subtitle = "Dummy data for Sprint 1",
                    DeltaText = "+5.3%",
                    IsPositiveDelta = true,
                    Accent = "cyan",
                    ValueElementId = "kpi-average-duration"
                },
                new()
                {
                    Title = "Peak Concurrent Players",
                    Value = "3",
                    Subtitle = "Dummy data for Sprint 1",
                    DeltaText = "+8.9%",
                    IsPositiveDelta = true,
                    Accent = "magenta",
                    ValueElementId = "kpi-peak-concurrent"
                }
            },
            Sessions = new List<LiveSessionRowViewModel>
            {
                new()
                {
                    SessionId = "90DD5B16-5195-4297-B36E-9C24DC91CBB0",
                    InstallationId = "C3FF90AA-C2C6-4C7E-A9FE-0E7E160537F2",
                    CurrentScene = "OutdoorsScene",
                    GameState = "Paused",
                    CurrentPhase = "ReceptionArrival",
                    LastHeartbeatLabel = "5s ago",
                    StatusLabel = "Active"
                },
                new()
                {
                    SessionId = "969F1B0B-CB21-498E-BCB6-5D0A1F7D9772",
                    InstallationId = "11111111-1111-1111-1111-111111111111",
                    CurrentScene = "MainMenu",
                    GameState = "Menu",
                    CurrentPhase = "Boot",
                    LastHeartbeatLabel = "12s ago",
                    StatusLabel = "Active"
                }
            }
        };

        return View(model);
    }
}