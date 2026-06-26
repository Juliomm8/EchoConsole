using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class UpdateIdentityViewModel
{
    [Required]
    [StringLength(32, MinimumLength = 3)]
    public string Alias { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string AvatarKey { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string Theme { get; set; } = string.Empty;

    [Required]
    [StringLength(8)]
    public string PreferredLanguage { get; set; } = string.Empty;
}
