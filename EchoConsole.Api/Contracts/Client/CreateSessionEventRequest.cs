using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Client;

public sealed class CreateSessionEventRequest
{
    [Required]
    [MaxLength(64)]
    public string EventType { get; set; } = null!;

    [MaxLength(128)]
    public string? Scene { get; set; }

    [MaxLength(64)]
    public string? GameState { get; set; }

    [MaxLength(64)]
    public string? Phase { get; set; }

    [MaxLength(4000)]
    public string? PayloadJson { get; set; }

    public DateTimeOffset? ClientTimeUtc { get; set; }
}