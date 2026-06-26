using System.Text.RegularExpressions;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Web.Services.Profile;

public static partial class ProfileCatalog
{
    public const string DefaultTheme = "green";
    public const string DefaultAvatarKey = "avatar_01";
    public const string DefaultLanguage = "en";

    public static IReadOnlyList<string> AvatarKeys { get; } =
        new[]
        {
            "avatar_01",
            "avatar_02",
            "avatar_03",
            "avatar_04",
            "avatar_05",
            "avatar_06"
        };

    public static IReadOnlyList<string> ThemeKeys { get; } =
        new[]
        {
            "green",
            "amber",
            "cyan",
            "monochrome"
        };

    public static IReadOnlyList<string> LanguageKeys { get; } =
        new[]
        {
            "en",
            "es"
        };

    private static readonly HashSet<string> AllowedThemes =
        new(ThemeKeys, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedAvatarKeys =
        new(AvatarKeys, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedLanguages =
        new(LanguageKeys, StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowedTheme(string value)
    {
        return AllowedThemes.Contains(value);
    }

    public static bool IsAllowedAvatarKey(string value)
    {
        return AllowedAvatarKeys.Contains(value);
    }

    public static bool IsAllowedLanguage(string value)
    {
        return AllowedLanguages.Contains(value);
    }

    public static bool IsValidAlias(string value)
    {
        return value.Length is >= 3 and <= 32 &&
            AliasRegex().IsMatch(value);
    }

    public static string NormalizeTheme(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeAvatarKey(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeLanguage(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeStoredAvatarKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "avatar_01" => "avatar_01",
            "avatar_02" => "avatar_02",
            "avatar_03" => "avatar_03",
            "avatar_04" => "avatar_04",
            "avatar_05" => "avatar_05",
            "avatar_06" => "avatar_06",
            "operator-01" or "avatar-01" => "avatar_01",
            "operator-02" or "avatar-02" => "avatar_02",
            "operator-03" or "avatar-03" => "avatar_03",
            "operator-04" or "avatar-04" => "avatar_04",
            "operator-05" or "avatar-05" => "avatar_05",
            "operator-06" or "avatar-06" => "avatar_06",
            _ => DefaultAvatarKey
        };
    }

    public static string NormalizeStoredTheme(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "green" or "phosphor-green" => "green",
            "amber" or "amber-monitor" => "amber",
            "cyan" or "cold-cyan" => "cyan",
            "monochrome" or "monochrome-crt" => "monochrome",
            _ => DefaultTheme
        };
    }

    public static string GetRoleDisplayName(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => "Administrador",
            UserRole.Moderator => "Supervisor Técnico",
            UserRole.Viewer => "Operador/Jugador",
            _ => "Operador/Jugador"
        };
    }

    [GeneratedRegex(
        "^[a-zA-Z0-9 _-]+$",
        RegexOptions.Compiled)]
    private static partial Regex AliasRegex();
}
