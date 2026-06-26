using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Web.Models.Profile;

public sealed class RevokeUserSessionViewModel
{
    [Range(1, long.MaxValue)]
    public long SessionId { get; set; }
}
