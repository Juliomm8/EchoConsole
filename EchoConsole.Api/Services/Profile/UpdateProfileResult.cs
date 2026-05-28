namespace EchoConsole.Api.Services.Profile;

public sealed class UpdateProfileResult
{
    public UpdateProfileStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public int UserId { get; init; }

    public string Alias { get; init; } = string.Empty;

    public string AvatarKey { get; init; } = string.Empty;

    public string Theme { get; init; } = string.Empty;

    public static UpdateProfileResult Success(
        int userId,
        string alias,
        string avatarKey,
        string theme)
        => new()
        {
            Status = UpdateProfileStatus.Success,
            Message = "Profile updated successfully.",
            UserId = userId,
            Alias = alias,
            AvatarKey = avatarKey,
            Theme = theme
        };

    public static UpdateProfileResult Failure(UpdateProfileStatus status, string message, int userId = 0)
        => new()
        {
            Status = status,
            Message = message,
            UserId = userId
        };
}