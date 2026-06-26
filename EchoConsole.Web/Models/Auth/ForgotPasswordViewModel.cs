using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Auth;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_Email")]
    [StringLength(
        256,
        ErrorMessage = "Validation_MaxLength")]
    [Display(Name = "Auth_EmailLabel")]
    public string Email { get; set; } =
        string.Empty;
}
