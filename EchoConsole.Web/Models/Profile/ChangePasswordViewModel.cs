using System.ComponentModel.DataAnnotations;
using EchoConsole.Web;

namespace EchoConsole.Web.Models.Profile;

public sealed class ChangePasswordViewModel
{
    [Required(
        ErrorMessageResourceType = typeof(SharedResource),
        ErrorMessageResourceName =
            "Auth_CurrentPasswordRequired")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(
        ErrorMessageResourceType = typeof(SharedResource),
        ErrorMessageResourceName =
            "Auth_NewPasswordRequired")]
    [DataType(DataType.Password)]
    [StringLength(
        100,
        MinimumLength = 8,
        ErrorMessageResourceType = typeof(SharedResource),
        ErrorMessageResourceName =
            "Auth_PasswordTooShort")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(
        ErrorMessageResourceType = typeof(SharedResource),
        ErrorMessageResourceName =
            "Auth_ConfirmPasswordRequired")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(NewPassword),
        ErrorMessageResourceType = typeof(SharedResource),
        ErrorMessageResourceName =
            "Auth_PasswordConfirmationMismatch")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
