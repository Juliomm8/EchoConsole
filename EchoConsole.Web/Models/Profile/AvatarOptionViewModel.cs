namespace EchoConsole.Web.Models.Profile;

public sealed class AvatarOptionViewModel
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ShortCode { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsSelected { get; set; }
}
