using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Auth;

public sealed class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } =
        string.Empty;

    [Required]
    public string Token { get; set; } =
        string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [DataType(DataType.Password)]
    [StringLength(
        100,
        MinimumLength = 8,
        ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Auth_NewPasswordLabel")]
    public string NewPassword { get; set; } =
        string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(NewPassword),
        ErrorMessage =
            "Auth_PasswordConfirmationMismatch")]
    [Display(Name = "Auth_ConfirmPasswordLabel")]
    public string ConfirmPassword { get; set; } =
        string.Empty;
}
