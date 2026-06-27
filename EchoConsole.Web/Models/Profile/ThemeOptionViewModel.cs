namespace EchoConsole.Web.Models.Profile;

public sealed class ThemeOptionViewModel
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsSelected { get; set; }
}
