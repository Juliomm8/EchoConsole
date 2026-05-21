using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class ClaimInstallationViewModel
{
    [Required]
    [Display(Name = "Installation ID")]
    public string InstallationId { get; set; } = string.Empty;

    public string? StatusMessage { get; set; }

    public bool IsSuccess { get; set; }
}