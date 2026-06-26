using System.Text.RegularExpressions;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Web.Services.Profile;

public static partial class ProfileCatalog
{
    public const string DefaultTheme = "phosphor-green";
    public const string DefaultAvatarKey = "operator-01";
    public const string DefaultLanguage = "en";

    private static readonly HashSet<string> AllowedThemes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "phosphor-green",
            "amber-monitor",
            "cold-cyan",
            "monochrome-crt"
        };

    private static readonly HashSet<string> AllowedAvatarKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "operator-01",
            "operator-02",
            "operator-03",
            "operator-04",
            "operator-05",
            "operator-06"
        };

    private static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "en",
            "es"
        };

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
        return value.Length is >= 3 and <= 32 && AliasRegex().IsMatch(value);
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

    [GeneratedRegex("^[a-zA-Z0-9 _-]+$", RegexOptions.Compiled)]
    private static partial Regex AliasRegex();
}
