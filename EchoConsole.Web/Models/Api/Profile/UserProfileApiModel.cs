namespace EchoConsole.Web.Models.Api.Profile;

public sealed class UserProfileApiModel
{
    public int UserId { get; set; }

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

    public DateTimeOffset? LastActivityUtc { get; set; }

    public string FavoriteBuild { get; set; } = "N/A";

    public IReadOnlyList<LinkedInstallationApiModel> Installations { get; set; } = Array.Empty<LinkedInstallationApiModel>();
}