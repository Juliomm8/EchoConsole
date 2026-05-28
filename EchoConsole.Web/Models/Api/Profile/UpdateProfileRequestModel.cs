namespace EchoConsole.Web.Models.Api.Profile;

public sealed class UpdateProfileRequestModel
{
    public string Alias { get; set; } = string.Empty;

    public string AvatarKey { get; set; } = string.Empty;

    public string Theme { get; set; } = string.Empty;
}