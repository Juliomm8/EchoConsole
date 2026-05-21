namespace EchoConsole.Api.Services.Ownership;

public enum ClaimInstallationStatus
{
    Success = 1,
    AlreadyOwnedByCurrentUser = 2,
    InstallationNotFound = 3,
    UserNotFound = 4,
    AlreadyOwnedByAnotherUser = 5
}