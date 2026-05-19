using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Auth;

public sealed class RegisterViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    [Display(Name = "Alias")]
    public string Alias { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}