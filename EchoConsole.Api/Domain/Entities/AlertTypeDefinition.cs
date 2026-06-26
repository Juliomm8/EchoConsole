using System.ComponentModel.DataAnnotations;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Api.Domain.Entities;

public sealed class AlertTypeDefinition
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Code { get; set; } = null!;

    [MaxLength(128)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string Description { get; set; } = null!;

    public AlertSeverity DefaultSeverity { get; set; } = AlertSeverity.Warning;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
