using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Client;

public sealed class StartSessionRequest
{
    [Required]
    public Guid InstallationId { get; set; }

    [Required, MaxLength(64)]
    public string GameCode { get; set; } = null!;

    [Required, MaxLength(32)]
    public string BuildVersion { get; set; } = null!;

    [Required, MaxLength(128)]
    public string CurrentScene { get; set; } = null!;

    [Required, MaxLength(64)]
    public string CurrentGameState { get; set; } = null!;

    [MaxLength(64)]
    public string? CurrentPhase { get; set; }
}