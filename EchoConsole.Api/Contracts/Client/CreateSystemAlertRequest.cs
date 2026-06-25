using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Client;

public sealed class CreateSystemAlertRequest
{
    [Required]
    [MaxLength(64)]
    public string GameCode { get; set; } = null!;

    [MaxLength(32)]
    public string? Severity { get; set; }

    [MaxLength(64)]
    public string? ErrorTypeCode { get; set; }

    [MaxLength(64)]
    public string? BuildVersion { get; set; }

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string Source { get; set; } = null!;

    [MaxLength(64)]
    public string? InstallationId { get; set; }
}
