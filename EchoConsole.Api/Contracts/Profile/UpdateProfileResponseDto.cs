namespace EchoConsole.Api.Contracts.Profile;

public sealed class UpdateProfileResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string AvatarKey { get; set; } = string.Empty;

    public string Theme { get; set; } = string.Empty;
}