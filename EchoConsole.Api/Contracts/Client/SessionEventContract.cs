namespace EchoConsole.Api.Contracts.Client;

public static class SessionEventContract
{
    public const int MaxEventTypeCharacters = 64;
    public const int MaxSceneCharacters = 128;
    public const int MaxGameStateCharacters = 64;
    public const int MaxPhaseCharacters = 64;

    public const int MaxPayloadCharacters = 4000;
    public const int MaxPayloadUtf8Bytes = 8192;
    public const long MaxRequestBodyBytes = 32768;
    public const int MaxPayloadJsonDepth = 16;

    public const string SceneChanged = "SceneChanged";
    public const string PhaseChanged = "PhaseChanged";
    public const string GameStateChanged = "GameStateChanged";
    public const string ObjectiveUpdated = "ObjectiveUpdated";
    public const string ItemCollected = "ItemCollected";
    public const string EnemyEncountered = "EnemyEncountered";
    public const string PlayerDamaged = "PlayerDamaged";
    public const string PlayerDied = "PlayerDied";

    private static readonly string[] OfficialEventTypesInternal =
    {
        SceneChanged,
        PhaseChanged,
        GameStateChanged,
        ObjectiveUpdated,
        ItemCollected,
        EnemyEncountered,
        PlayerDamaged,
        PlayerDied
    };

    private static readonly IReadOnlyDictionary<string, string> CanonicalEventTypes =
        OfficialEventTypesInternal.ToDictionary(
            value => value,
            value => value,
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> OfficialEventTypes { get; } =
        Array.AsReadOnly(OfficialEventTypesInternal);

    public static bool TryNormalizeEventType(
        string? value,
        out string canonicalEventType)
    {
        canonicalEventType = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!CanonicalEventTypes.TryGetValue(
                value.Trim(),
                out var matchedEventType))
        {
            return false;
        }

        canonicalEventType = matchedEventType;
        return true;
    }
}