using System.Text.RegularExpressions;
using EchoConsole.Api.Contracts.Profile;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Services.Profile;

public sealed partial class UserProfileSettingsService : IUserProfileSettingsService
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cyan",
        "fuchsia",
        "amber"
    };

    private static readonly HashSet<string> AllowedAvatarKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "avatar-01",
        "avatar-02",
        "avatar-03",
        "avatar-04",
        "avatar-05"
    };

    private readonly EchoConsoleDbContext _dbContext;
    private readonly ILogger<UserProfileSettingsService> _logger;

    public UserProfileSettingsService(
        EchoConsoleDbContext dbContext,
        ILogger<UserProfileSettingsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UpdateProfileResult> UpdateProfileAsync(
        int userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var alias = request.Alias.Trim();
        var avatarKey = request.AvatarKey.Trim();
        var theme = request.Theme.Trim();

        if (string.IsNullOrWhiteSpace(alias))
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.AliasRequired,
                "Alias is required.",
                userId);
        }

        if (alias.Length < 3 || alias.Length > 32)
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.AliasInvalidLength,
                "Alias must be between 3 and 32 characters.",
                userId);
        }

        if (!AliasRegex().IsMatch(alias))
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.AliasInvalidCharacters,
                "Alias can only contain letters, numbers, spaces, hyphens and underscores.",
                userId);
        }

        if (!AllowedAvatarKeys.Contains(avatarKey))
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.InvalidAvatarKey,
                "Selected avatar is not valid.",
                userId);
        }

        if (!AllowedThemes.Contains(theme))
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.InvalidTheme,
                "Selected theme is not valid.",
                userId);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.UserNotFound,
                "User was not found.",
                userId);
        }

        var normalizedAlias = alias.ToUpperInvariant();

        var aliasExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != userId
                     && x.Alias != null
                     && x.Alias.ToUpper() == normalizedAlias,
                cancellationToken);

        if (aliasExists)
        {
            return UpdateProfileResult.Failure(
                UpdateProfileStatus.AliasAlreadyTaken,
                "Alias is already in use.",
                userId);
        }

        user.Alias = alias;
        user.AvatarKey = avatarKey;
        user.Theme = theme.ToLowerInvariant();

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} updated profile settings. Alias={Alias}, AvatarKey={AvatarKey}, Theme={Theme}.",
            userId,
            alias,
            avatarKey,
            theme);

        return UpdateProfileResult.Success(
            user.Id,
            user.Alias,
            user.AvatarKey,
            user.Theme);
    }

    [GeneratedRegex("^[a-zA-Z0-9 _-]+$", RegexOptions.Compiled)]
    private static partial Regex AliasRegex();
}