namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileSettingsViewModel
{
    public IdentityTabViewModel Identity { get; set; } = new();

    public IReadOnlyList<AvatarOptionViewModel> AvatarOptions { get; set; } =
        Array.Empty<AvatarOptionViewModel>();

    public IReadOnlyList<ThemeOptionViewModel> ThemeOptions { get; set; } =
        Array.Empty<ThemeOptionViewModel>();
}
