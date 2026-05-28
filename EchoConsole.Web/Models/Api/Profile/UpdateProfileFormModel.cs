using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class UpdateProfileFormModel
{
    [Required]
    [StringLength(32, MinimumLength = 3)]
    public string Alias { get; set; } = string.Empty;

    [Required]
    public string AvatarKey { get; set; } = string.Empty;

    [Required]
    public string Theme { get; set; } = string.Empty;
}