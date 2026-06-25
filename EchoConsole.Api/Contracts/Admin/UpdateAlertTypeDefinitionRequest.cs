using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Admin;

public sealed class UpdateAlertTypeDefinitionRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string DefaultSeverity { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
