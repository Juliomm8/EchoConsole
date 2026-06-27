using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Auth;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(100, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Auth_NameLabel")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [StringLength(64, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Auth_AliasLabel")]
    public string Alias { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_Email")]
    [StringLength(256, ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Auth_EmailLabel")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Auth_PasswordLabel")]
    public string Password { get; set; } = string.Empty;
}
