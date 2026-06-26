using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class UnlinkDeviceViewModel
{
    [Required]
    public Guid InstallationId { get; set; }
}
