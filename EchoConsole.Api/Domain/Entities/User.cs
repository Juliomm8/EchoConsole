using System.ComponentModel.DataAnnotations;
using EchoConsole.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace EchoConsole.Api.Domain.Entities;

public sealed class User : IdentityUser<int>
{
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(64)]
    public string Alias { get; set; } = null!;

    [MaxLength(128)]
    public string AvatarKey { get; set; } = "avatar-01";

    [MaxLength(32)]
    public string Theme { get; set; } = "cyan";

    [MaxLength(8)]
    public string PreferredLanguage { get; set; } = "en";

    public DateTimeOffset? ProfileUpdatedAtUtc { get; set; }

    public UserRole Role { get; set; } = UserRole.Viewer;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<Installation> Installations { get; set; } =
        new List<Installation>();

    public ICollection<UserSession> UserSessions { get; set; } =
        new List<UserSession>();
}
