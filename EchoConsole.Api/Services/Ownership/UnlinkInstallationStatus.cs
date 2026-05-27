namespace EchoConsole.Api.Services.Ownership;

public enum UnlinkInstallationStatus
{
    Success = 1,
    InstallationNotFound = 2,
    UserNotFound = 3,
    AlreadyUnlinked = 4,
    NotOwner = 5
}