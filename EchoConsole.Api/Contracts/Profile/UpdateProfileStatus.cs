namespace EchoConsole.Api.Services.Profile;

public enum UpdateProfileStatus
{
    Success = 1,
    UserNotFound = 2,
    AliasRequired = 3,
    AliasInvalidLength = 4,
    AliasInvalidCharacters = 5,
    AliasAlreadyTaken = 6,
    InvalidAvatarKey = 7,
    InvalidTheme = 8
}