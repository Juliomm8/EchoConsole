using System.Text.RegularExpressions;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Web.Services.Profile;

public static partial class ProfileCatalog
{
    public const string DefaultTheme = "phosphor-green";
    public const string DefaultAvatarKey = "operator-01";
    public const string DefaultLanguage = "en";

    public static IReadOnlyList<string> AvatarKeys { get; } =
        new[]
        {
            "operator-01",
            "operator-02",
            "operator-03",
            "operator-04",
            "operator-05",
            "operator-06"
        };

    public static IReadOnlyList<string> ThemeKeys { get; } =
        new[]
        {
            "phosphor-green",
            "amber-monitor",
            "cold-cyan",
            "monochrome-crt"
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
            "operator-01" or "operator-02" or "operator-03" or
            "operator-04" or "operator-05" or "operator-06" =>
                value.Trim().ToLowerInvariant(),
            "avatar-01" => "operator-01",
            "avatar-02" => "operator-02",
            "avatar-03" => "operator-03",
            "avatar-04" => "operator-04",
            "avatar-05" => "operator-05",
            "avatar-06" => "operator-06",
            _ => DefaultAvatarKey
        };
    }

    public static string NormalizeStoredTheme(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "phosphor-green" or "amber-monitor" or
            "cold-cyan" or "monochrome-crt" =>
                value.Trim().ToLowerInvariant(),
            "amber" => "amber-monitor",
            "cyan" => "cold-cyan",
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
