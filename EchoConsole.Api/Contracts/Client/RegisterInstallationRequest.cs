using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Client;

public sealed class RegisterInstallationRequest
{
    [Required]
    public Guid InstallationId { get; set; }

    [Required, MaxLength(64)]
    public string GameCode { get; set; } = null!;

    [Required, MaxLength(32)]
    public string BuildVersion { get; set; } = null!;

    [Required, MaxLength(32)]
    public string Platform { get; set; } = null!;

    [Required, MaxLength(128)]
    public string DeviceName { get; set; } = null!;

    [Required, MaxLength(128)]
    public string DeviceModel { get; set; } = null!;

    [Required, MaxLength(128)]
    public string OperatingSystem { get; set; } = null!;
}