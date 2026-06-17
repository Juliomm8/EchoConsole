using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Client;

public sealed class CreateSessionEventRequest
{
    [Required]
    [StringLength(
        SessionEventContract.MaxEventTypeCharacters,
        MinimumLength = 1)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(SessionEventContract.MaxSceneCharacters)]
    public string? Scene { get; set; }

    [StringLength(SessionEventContract.MaxGameStateCharacters)]
    public string? GameState { get; set; }

    [StringLength(SessionEventContract.MaxPhaseCharacters)]
    public string? Phase { get; set; }

    [StringLength(SessionEventContract.MaxPayloadCharacters)]
    public string? PayloadJson { get; set; }

    public DateTimeOffset? ClientTimeUtc { get; set; }
}