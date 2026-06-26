namespace EchoConsole.Web.Models.Profile;

public sealed class ProfileViewModel : ProfileIndexViewModel
{
    public IdentityTabViewModel IdentityPreview { get; set; } = new();

    public string InitialSection { get; set; } = "identity";
}
