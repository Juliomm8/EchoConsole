namespace EchoConsole.Web.Models.Api.Profile;

public sealed class UnlinkInstallationRequestModel
{
    public Guid InstallationId { get; set; }

    public int UserId { get; set; }
}