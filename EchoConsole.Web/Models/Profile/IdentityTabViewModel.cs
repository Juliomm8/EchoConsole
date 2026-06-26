namespace EchoConsole.Web.Models.Profile;

public sealed class IdentityTabViewModel
{
    public int UserId { get; set; }

    public string Alias { get; set; } = "Player";

    public string Email { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AvatarKey { get; set; } = "operator-01";

    public string Theme { get; set; } = "phosphor-green";

    public string PreferredLanguage { get; set; } = "en";

    public string Role { get; set; } = "Viewer";

    public string RoleDisplayName { get; set; } = "Operator / Player";

    public string Status { get; set; } = "Active";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    public int LinkedNodeCount { get; set; }

    public int TotalSessions { get; set; }

    public long TotalPlayTimeMinutes { get; set; }

    public double TotalPlayTimeHours { get; set; }

    public int LongestSessionMinutes { get; set; }

    public string FavoriteBuild { get; set; } = "N/A";
}
