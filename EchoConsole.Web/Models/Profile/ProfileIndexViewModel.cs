namespace EchoConsole.Web.Models.Profile;

public class ProfileIndexViewModel
{
    public string Alias { get; set; } = "Player";

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AvatarKey { get; set; } = "avatar-01";

    public string Theme { get; set; } = "cyan";

    public string Role { get; set; } = "Viewer";

    public string Status { get; set; } = "Active";

    public int TotalInstallations { get; set; }

    public int TotalSessions { get; set; }

    public int TotalPlayTimeMinutes { get; set; }

    public string TotalPlayTimeLabel { get; set; } = "0m";

    public string LastActivityLabel { get; set; } = "N/A";

    public string FavoriteBuild { get; set; } = "N/A";

    public IReadOnlyList<LinkedInstallationViewModel> Installations { get; set; } = Array.Empty<LinkedInstallationViewModel>();
}