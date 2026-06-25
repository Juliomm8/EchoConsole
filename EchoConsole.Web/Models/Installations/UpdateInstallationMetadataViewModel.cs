using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Installations;

public sealed class UpdateInstallationMetadataViewModel
{
    [Required]
    public Guid InstallationId { get; set; }

    [StringLength(128)]
    public string? AdminAlias { get; set; }

    [Required]
    [StringLength(24)]
    public string AdminStatus { get; set; } = "Active";
}
