using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoConsole.Api.Seed;

public sealed class DevelopmentDataSeeder
{
    private readonly EchoConsoleDbContext _dbContext;
    private readonly DemoSeedOptions _options;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    public DevelopmentDataSeeder(
        EchoConsoleDbContext dbContext,
        IOptions<DemoSeedOptions> options,
        ILogger<DevelopmentDataSeeder> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Demo seed skipped because DemoSeed:Enabled is false.");

            return;
        }

        await _dbContext.Database.MigrateAsync(cancellationToken);

        if (_options.ResetBeforeSeed)
        {
            _logger.LogInformation(
                "ResetBeforeSeed is enabled. Clearing demo data...");

            if (_dbContext.Model.FindEntityType(typeof(UserSession)) is not null)
            {
                _dbContext.UserSessions.RemoveRange(
                    _dbContext.UserSessions);
            }

            if (_dbContext.Model.FindEntityType(typeof(GameSession)) is not null)
            {
                _dbContext.GameSessions.RemoveRange(
                    _dbContext.GameSessions);
            }

            if (_dbContext.Model.FindEntityType(typeof(SystemAlert)) is not null)
            {
                _dbContext.SystemAlerts.RemoveRange(
                    _dbContext.SystemAlerts);
            }

            if (_dbContext.Model.FindEntityType(typeof(Installation)) is not null)
            {
                _dbContext.Installations.RemoveRange(
                    _dbContext.Installations);
            }

            if (_dbContext.Model.FindEntityType(typeof(GameBuild)) is not null)
            {
                _dbContext.GameBuilds.RemoveRange(
                    _dbContext.GameBuilds);
            }

            if (_dbContext.Model.FindEntityType(typeof(User)) is not null)
            {
                _dbContext.Users.RemoveRange(
                    _dbContext.Users);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;

        await SeedGameBuildsAsync(now, cancellationToken);
        await SeedInstallationsAsync(now, cancellationToken);
        await SeedGameSessionsAsync(now, cancellationToken);
        await SeedSystemAlertsAsync(now, cancellationToken);
        await SeedDevelopmentUsersAsync(now, cancellationToken);

        _logger.LogInformation(
            "Demo data seeding completed successfully.");
    }

    private async Task SeedGameBuildsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.GameBuilds.AnyAsync(cancellationToken))
        {
            return;
        }

        var buildVersions = new[]
        {
            "0.1.0-dev",
            "0.2.0-alpha",
            "0.3.0-alpha",
            "0.4.0-beta",
            "0.5.0-beta",
            "1.0.0-rc1"
        };

        var builds = new List<GameBuild>();

        for (var index = 0; index < buildVersions.Length; index++)
        {
            var version = buildVersions[index];

            builds.Add(
                new GameBuild
                {
                    VersionNumber = version,
                    ReleaseNotes =
                        $"Demo release notes for {version}",
                    ReleaseDateUtc = now.AddDays(
                        -(30 - index * 5)),
                    IsActive = index == buildVersions.Length - 1,
                    EngineVersion = "Unity 2022.3.62f2"
                });
        }

        _dbContext.GameBuilds.AddRange(builds);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInstallationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.Installations.AnyAsync(cancellationToken))
        {
            return;
        }

        var installations = new List<Installation>
        {
            new()
            {
                InstallationId = Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),
                GameCode = "cosmic-diner",
                BuildVersion = "1.0.0-rc1",
                Platform = "WindowsPlayer",
                DeviceName = "JEREMY-PC",
                DeviceModel = "Desktop",
                OSVersion = "Windows 11",
                Processor = "Intel Core i7-12700H",
                Gpu = "NVIDIA RTX 3060",
                RamMb = 16384,
                Status = "Active",
                FirstSeenUtc = now.AddDays(-12),
                LastUpdateUtc = now.AddMinutes(-1)
            },
            new()
            {
                InstallationId = Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),
                GameCode = "cosmic-diner",
                BuildVersion = "0.5.0-beta",
                Platform = "WindowsPlayer",
                DeviceName = "ASUS-F15",
                DeviceModel = "Laptop",
                OSVersion = "Windows 11",
                Processor = "Intel Core i5-11400H",
                Gpu = "NVIDIA GTX 1650",
                RamMb = 8192,
                Status = "Active",
                FirstSeenUtc = now.AddDays(-9),
                LastUpdateUtc = now.AddMinutes(-3)
            },
            new()
            {
                InstallationId = Guid.Parse(
                    "33333333-3333-3333-3333-333333333333"),
                GameCode = "cosmic-diner",
                BuildVersion = "0.4.0-beta",
                Platform = "WindowsPlayer",
                DeviceName = "TEST-RIG-01",
                DeviceModel = "Desktop",
                OSVersion = "Windows 10",
                Processor = "AMD Ryzen 5 5600X",
                Gpu = "AMD RX 6600",
                RamMb = 16384,
                Status = "Active",
                FirstSeenUtc = now.AddDays(-20),
                LastUpdateUtc = now.AddHours(-2)
            }
        };

        _dbContext.Installations.AddRange(installations);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedGameSessionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.GameSessions.AnyAsync(cancellationToken))
        {
            return;
        }

        var installations = await _dbContext.Installations
            .OrderBy(installation => installation.Id)
            .ToListAsync(cancellationToken);

        if (installations.Count < 3)
        {
            return;
        }

        var sessions = new List<GameSession>
        {
            new()
            {
                SessionId = Guid.NewGuid(),
                InstallationDbId = installations[0].Id,
                SessionTokenHash = Guid.NewGuid().ToString("N"),
                BuildVersion = installations[0].BuildVersion,
                CurrentScene = "OutdoorsScene",
                CurrentGameState = "Playing",
                CurrentPhase = "InvestigationStart",
                StartedAtUtc = now.AddMinutes(-28),
                LastHeartbeatUtc = now.AddSeconds(-10),
                Status = SessionStatus.Active
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                InstallationDbId = installations[1].Id,
                SessionTokenHash = Guid.NewGuid().ToString("N"),
                BuildVersion = installations[1].BuildVersion,
                CurrentScene = "MainMenu",
                CurrentGameState = "Menu",
                CurrentPhase = "Boot",
                StartedAtUtc = now.AddMinutes(-6),
                LastHeartbeatUtc = now.AddSeconds(-18),
                Status = SessionStatus.Active
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                InstallationDbId = installations[2].Id,
                SessionTokenHash = Guid.NewGuid().ToString("N"),
                BuildVersion = installations[2].BuildVersion,
                CurrentScene = "KitchenHall",
                CurrentGameState = "Paused",
                CurrentPhase = "PowerRestored",
                StartedAtUtc = now.AddMinutes(-43),
                LastHeartbeatUtc = now.AddMinutes(-20),
                EndedAtUtc = now.AddMinutes(-18),
                Status = SessionStatus.Ended
            }
        };

        _dbContext.GameSessions.AddRange(sessions);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSystemAlertsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.SystemAlerts.AnyAsync(cancellationToken))
        {
            return;
        }

        var installationGuid = await _dbContext.Installations
            .OrderBy(installation => installation.Id)
            .Select(installation => installation.InstallationId)
            .FirstOrDefaultAsync(cancellationToken);

        var installationId = installationGuid == Guid.Empty
            ? null
            : installationGuid.ToString().ToUpperInvariant();

        var alerts = new List<SystemAlert>
        {
            new()
            {
                Severity = AlertSeverity.Info,
                Message = "Demo seed initialized successfully.",
                Source = "Server",
                InstallationId = null,
                CreatedAtUtc = now.AddHours(-10),
                IsResolved = true,
                ResolvedAtUtc = now.AddHours(-9)
            },
            new()
            {
                Severity = AlertSeverity.Warning,
                Message =
                    "Missed heartbeat detected for one client session.",
                Source = "GameClient",
                InstallationId = installationId,
                CreatedAtUtc = now.AddHours(-4),
                IsResolved = false
            }
        };

        _dbContext.SystemAlerts.AddRange(alerts);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDevelopmentUsersAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .OrderBy(user => user.Id)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            users.AddRange(
                CreateDemoUsers(now));

            _dbContext.Users.AddRange(users);
        }

        var changedUsers = 0;

        foreach (var user in users)
        {
            var changed = PrepareDevelopmentUser(user);

            if (changed)
            {
                changedUsers++;
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Development identity seed completed. Users={UserCount}, Updated={UpdatedCount}, EmailConfirmed=true.",
            users.Count,
            changedUsers);
    }

    private static IReadOnlyList<User> CreateDemoUsers(
        DateTimeOffset now)
    {
        return new List<User>
        {
            CreateDemoUser(
                "Jeremy Tomaselly",
                "jeremy@echoconsole.local",
                "jeremy_dev",
                "avatar-01",
                "cyan",
                UserRole.Admin,
                now.AddDays(-20)),
            CreateDemoUser(
                "Julio Mera",
                "julio@echoconsole.local",
                "julio_lead",
                "avatar-02",
                "fuchsia",
                UserRole.Admin,
                now.AddDays(-19)),
            CreateDemoUser(
                "Samuel Cobo",
                "samuel@echoconsole.local",
                "samuel_mod",
                "avatar-03",
                "amber",
                UserRole.Moderator,
                now.AddDays(-17)),
            CreateDemoUser(
                "Povea",
                "povea@echoconsole.local",
                "povea",
                "avatar-04",
                "cyan",
                UserRole.Viewer,
                now.AddDays(-15)),
            CreateDemoUser(
                "Armas",
                "armas@echoconsole.local",
                "armas",
                "avatar-05",
                "cyan",
                UserRole.Viewer,
                now.AddDays(-14))
        };
    }

    private static User CreateDemoUser(
        string name,
        string email,
        string alias,
        string avatarKey,
        string theme,
        UserRole role,
        DateTimeOffset createdAtUtc)
    {
        var normalizedEmail = email.ToUpperInvariant();

        return new User
        {
            Name = name,
            Email = email,
            UserName = email,
            NormalizedEmail = normalizedEmail,
            NormalizedUserName = normalizedEmail,
            EmailConfirmed = true,
            Alias = alias,
            AvatarKey = avatarKey,
            Theme = theme,
            PreferredLanguage = "es",
            ProfileUpdatedAtUtc = createdAtUtc,
            Role = role,
            Status = UserStatus.Active,
            CreatedAtUtc = createdAtUtc,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }

    private static bool PrepareDevelopmentUser(User user)
    {
        var changed = false;

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.UserName) &&
            !string.IsNullOrWhiteSpace(user.Email))
        {
            user.UserName = user.Email;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.NormalizedEmail) &&
            !string.IsNullOrWhiteSpace(user.Email))
        {
            user.NormalizedEmail = user.Email.ToUpperInvariant();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.NormalizedUserName) &&
            !string.IsNullOrWhiteSpace(user.UserName))
        {
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.AvatarKey))
        {
            user.AvatarKey = "avatar-01";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.Theme))
        {
            user.Theme = "cyan";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.PreferredLanguage))
        {
            user.PreferredLanguage = "es";
            changed = true;
        }

        if (!user.ProfileUpdatedAtUtc.HasValue)
        {
            user.ProfileUpdatedAtUtc = user.CreatedAtUtc;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.ConcurrencyStamp))
        {
            user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            changed = true;
        }

        return changed;
    }
}
