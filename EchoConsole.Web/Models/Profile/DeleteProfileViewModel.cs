using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class DeleteProfileViewModel
{
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string ConfirmationText { get; set; } = string.Empty;
}
