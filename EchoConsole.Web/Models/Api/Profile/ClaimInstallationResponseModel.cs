namespace EchoConsole.Web.Models.Api.Profile;

public sealed class ClaimInstallationResponseModel
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public Guid InstallationId { get; set; }

    public int? OwnerUserId { get; set; }
}