using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Client;

public sealed class EndSessionRequest
{
    [Required, MaxLength(64)]
    public string Reason { get; set; } = "ApplicationQuit";

    [Required, MaxLength(128)]
    public string CurrentScene { get; set; } = null!;

    [Required, MaxLength(64)]
    public string CurrentGameState { get; set; } = null!;

    [MaxLength(64)]
    public string? CurrentPhase { get; set; }

    public DateTimeOffset ClientTimeUtc { get; set; }
}