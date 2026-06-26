using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EchoConsole.Web.Hubs;

[Authorize]
public sealed class ProfileTelemetryHub : Hub
{
    private const string UserGroupPrefix = "profile-user:";

    public override async Task OnConnectedAsync()
    {
        var userIdValue = Context.User?
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetUserGroupName(userId));

        await base.OnConnectedAsync();
    }

    public static string GetUserGroupName(int userId)
    {
        return $"{UserGroupPrefix}{userId}";
    }
}
