using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Profile;

public sealed class UpdateProfileRequest
{
    [Required]
    [StringLength(32, MinimumLength = 3)]
    public string Alias { get; set; } = string.Empty;

    [Required]
    public string AvatarKey { get; set; } = string.Empty;

    [Required]
    public string Theme { get; set; } = string.Empty;
}