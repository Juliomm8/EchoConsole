using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Admin;

public sealed class UpdateInstallationAdminMetadataRequest
{
    [MaxLength(128)]
    public string? AdminAlias { get; set; }

    [Required]
    [MaxLength(24)]
    public string AdminStatus { get; set; } = "Active";
}
