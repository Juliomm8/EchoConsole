namespace EchoConsole.Api.Services.Ownership;

public sealed class ClaimInstallationResult
{
    public ClaimInstallationStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public Guid InstallationId { get; init; }

    public int? OwnerUserId { get; init; }

    public static ClaimInstallationResult Success(Guid installationId, int ownerUserId)
        => new()
        {
            Status = ClaimInstallationStatus.Success,
            Message = "Installation successfully linked to the user.",
            InstallationId = installationId,
            OwnerUserId = ownerUserId
        };

    public static ClaimInstallationResult AlreadyOwnedByCurrentUser(Guid installationId, int ownerUserId)
        => new()
        {
            Status = ClaimInstallationStatus.AlreadyOwnedByCurrentUser,
            Message = "This installation is already linked to your account.",
            InstallationId = installationId,
            OwnerUserId = ownerUserId
        };

    public static ClaimInstallationResult InstallationNotFound(Guid installationId)
        => new()
        {
            Status = ClaimInstallationStatus.InstallationNotFound,
            Message = "Installation was not found.",
            InstallationId = installationId
        };

    public static ClaimInstallationResult UserNotFound(Guid installationId)
        => new()
        {
            Status = ClaimInstallationStatus.UserNotFound,
            Message = "User was not found.",
            InstallationId = installationId
        };

    public static ClaimInstallationResult AlreadyOwnedByAnotherUser(Guid installationId, int? ownerUserId)
        => new()
        {
            Status = ClaimInstallationStatus.AlreadyOwnedByAnotherUser,
            Message = "This installation is already linked to another account.",
            InstallationId = installationId,
            OwnerUserId = ownerUserId
        };
}