namespace EchoConsole.Api.Services.Ownership;

public sealed class UnlinkInstallationResult
{
    public UnlinkInstallationStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public Guid InstallationId { get; init; }

    public int? PreviousOwnerUserId { get; init; }

    public static UnlinkInstallationResult Success(Guid installationId, int previousOwnerUserId)
        => new()
        {
            Status = UnlinkInstallationStatus.Success,
            Message = "Installation successfully unlinked from your account.",
            InstallationId = installationId,
            PreviousOwnerUserId = previousOwnerUserId
        };

    public static UnlinkInstallationResult InstallationNotFound(Guid installationId)
        => new()
        {
            Status = UnlinkInstallationStatus.InstallationNotFound,
            Message = "Installation was not found.",
            InstallationId = installationId
        };

    public static UnlinkInstallationResult UserNotFound(Guid installationId)
        => new()
        {
            Status = UnlinkInstallationStatus.UserNotFound,
            Message = "User was not found.",
            InstallationId = installationId
        };

    public static UnlinkInstallationResult AlreadyUnlinked(Guid installationId)
        => new()
        {
            Status = UnlinkInstallationStatus.AlreadyUnlinked,
            Message = "This installation is already unlinked.",
            InstallationId = installationId
        };

    public static UnlinkInstallationResult NotOwner(Guid installationId, int? previousOwnerUserId)
        => new()
        {
            Status = UnlinkInstallationStatus.NotOwner,
            Message = "You are not allowed to unlink this installation.",
            InstallationId = installationId,
            PreviousOwnerUserId = previousOwnerUserId
        };
}