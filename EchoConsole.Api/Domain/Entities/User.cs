using System.ComponentModel.DataAnnotations;
using EchoConsole.Api.Domain.Enums;

namespace EchoConsole.Api.Domain.Entities;

public sealed class User
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(256)]
    public string Email { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Viewer;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; }
}