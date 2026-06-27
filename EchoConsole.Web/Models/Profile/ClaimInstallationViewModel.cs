using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class ClaimInstallationViewModel
{
    [Required]
    public Guid InstallationId { get; set; }
}
