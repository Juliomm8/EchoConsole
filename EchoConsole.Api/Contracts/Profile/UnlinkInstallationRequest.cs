using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Profile;

public sealed class UnlinkInstallationRequest
{
    [Required]
    public Guid InstallationId { get; set; }

    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
}