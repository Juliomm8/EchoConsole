using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Auth;

public sealed class VerifyOtpViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "INGRESE EL CÓDIGO DE VERIFICACIÓN")]
    [RegularExpression(
        @"^\d{6}$",
        ErrorMessage = "EL CÓDIGO DEBE CONTENER 6 DÍGITOS")]
    [StringLength(
        6,
        MinimumLength = 6,
        ErrorMessage = "EL CÓDIGO DEBE CONTENER 6 DÍGITOS")]
    public string Code { get; set; } = string.Empty;
}
