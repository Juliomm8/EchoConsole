namespace EchoConsole.Web.Models.Profile;

public sealed class SecurityTabViewModel
{
    public bool HasLocalPassword { get; set; }

    public bool EmailConfirmed { get; set; }

    public int ActiveSessionCount { get; set; }

    public IReadOnlyList<ActiveUserSessionViewModel> ActiveSessions { get; set; } =
        Array.Empty<ActiveUserSessionViewModel>();
}
