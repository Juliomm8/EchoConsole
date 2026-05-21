using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EchoConsole.Web.Hubs;

[Authorize(Roles = "Admin")]
public sealed class AdminTelemetryHub : Hub
{
}