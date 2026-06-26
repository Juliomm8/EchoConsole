namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileDashboardViewModel
{
    public int UserId { get; set; }

    public string Alias { get; set; } = "Player";

    public string Name { get; set; } = string.Empty;

    public string AvatarKey { get; set; } = "avatar_01";

    public string RoleDisplayName { get; set; } = "Operador/Jugador";

    public string Status { get; set; } = "Active";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
