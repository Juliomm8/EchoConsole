using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Demo seed skipped because DemoSeed:Enabled is false.");
            return;
        }

        await _dbContext.Database.MigrateAsync(cancellationToken);

        if (_options.ResetBeforeSeed)
        {
            _logger.LogInformation("ResetBeforeSeed is enabled. Clearing demo data...");

            if (_dbContext.Model.FindEntityType(typeof(GameSession)) is not null)
                _dbContext.GameSessions.RemoveRange(_dbContext.GameSessions);

            if (_dbContext.Model.FindEntityType(typeof(SystemAlert)) is not null)
                _dbContext.SystemAlerts.RemoveRange(_dbContext.SystemAlerts);

            if (_dbContext.Model.FindEntityType(typeof(Installation)) is not null)
                _dbContext.Installations.RemoveRange(_dbContext.Installations);

            if (_dbContext.Model.FindEntityType(typeof(GameBuild)) is not null)
                _dbContext.GameBuilds.RemoveRange(_dbContext.GameBuilds);

            if (_dbContext.Model.FindEntityType(typeof(User)) is not null)
                _dbContext.Set<User>().RemoveRange(_dbContext.Set<User>());

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;

        var buildVersions = new[] { "0.1.0-dev", "0.2.0-alpha", "0.3.0-alpha", "0.4.0-beta", "0.5.0-beta", "1.0.0-rc1" };

        if (!_dbContext.GameBuilds.Any())
        {
            var builds = new List<GameBuild>();
            for (var i = 0; i < buildVersions.Length; i++)
            {
                builds.Add(new GameBuild
                {
                    VersionNumber = buildVersions[i],
                    ReleaseNotes = $"Demo release notes for {buildVersions[i]}",
                    ReleaseDateUtc = now.AddDays(-(30 - i * 5)),
                    IsActive = i == buildVersions.Length - 1,
                    EngineVersion = "Unity 2022.3.62f2"
                });
            }
            _dbContext.GameBuilds.AddRange(builds);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!_dbContext.Installations.Any())
        {
            var installations = new List<Installation>
            {
                new() { InstallationId = Guid.Parse("11111111-1111-1111-1111-111111111111"), GameCode = "cosmic-diner", BuildVersion = "1.0.0-rc1", Platform = "WindowsPlayer", DeviceName = "JEREMY-PC", DeviceModel = "Desktop", OSVersion = "Windows 11", Processor = "Intel Core i7-12700H", Gpu = "NVIDIA RTX 3060", RamMb = 16384, Status = "Active", FirstSeenUtc = now.AddDays(-12), LastUpdateUtc = now.AddMinutes(-1) },
                new() { InstallationId = Guid.Parse("22222222-2222-2222-2222-222222222222"), GameCode = "cosmic-diner", BuildVersion = "0.5.0-beta", Platform = "WindowsPlayer", DeviceName = "ASUS-F15", DeviceModel = "Laptop", OSVersion = "Windows 11", Processor = "Intel Core i5-11400H", Gpu = "NVIDIA GTX 1650", RamMb = 8192, Status = "Active", FirstSeenUtc = now.AddDays(-9), LastUpdateUtc = now.AddMinutes(-3) },
                new() { InstallationId = Guid.Parse("33333333-3333-3333-3333-333333333333"), GameCode = "cosmic-diner", BuildVersion = "0.4.0-beta", Platform = "WindowsPlayer", DeviceName = "TEST-RIG-01", DeviceModel = "Desktop", OSVersion = "Windows 10", Processor = "AMD Ryzen 5 5600X", Gpu = "AMD RX 6600", RamMb = 16384, Status = "Active", FirstSeenUtc = now.AddDays(-20), LastUpdateUtc = now.AddHours(-2) }
            };
            _dbContext.Installations.AddRange(installations);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!_dbContext.GameSessions.Any())
        {
            var installations = await _dbContext.Installations.OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var sessions = new List<GameSession>();

            if (installations.Count >= 3)
            {
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[0].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[0].BuildVersion, CurrentScene = "OutdoorsScene", CurrentGameState = "Playing", CurrentPhase = "InvestigationStart", StartedAtUtc = now.AddMinutes(-28), LastHeartbeatUtc = now.AddSeconds(-10), Status = SessionStatus.Active });
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[1].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[1].BuildVersion, CurrentScene = "MainMenu", CurrentGameState = "Menu", CurrentPhase = "Boot", StartedAtUtc = now.AddMinutes(-6), LastHeartbeatUtc = now.AddSeconds(-18), Status = SessionStatus.Active });
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[2].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[2].BuildVersion, CurrentScene = "KitchenHall", CurrentGameState = "Paused", CurrentPhase = "PowerRestored", StartedAtUtc = now.AddMinutes(-43), LastHeartbeatUtc = now.AddMinutes(-20), EndedAtUtc = now.AddMinutes(-18), Status = SessionStatus.Ended });
            }
            _dbContext.GameSessions.AddRange(sessions);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!_dbContext.SystemAlerts.Any())
        {
            var installations = await _dbContext.Installations.OrderBy(x => x.Id).ToListAsync(cancellationToken);
            string? installationId1 = installations.ElementAtOrDefault(0)?.InstallationId.ToString().ToUpperInvariant();

            var alerts = new List<SystemAlert>
            {
                new() { Severity = AlertSeverity.Info, Message = "Demo seed initialized successfully.", Source = "Server", InstallationId = null, CreatedAtUtc = now.AddHours(-10), IsResolved = true, ResolvedAtUtc = now.AddHours(-9) },
                new() { Severity = AlertSeverity.Warning, Message = "Missed heartbeat detected for one client session.", Source = "GameClient", InstallationId = installationId1, CreatedAtUtc = now.AddHours(-4), IsResolved = false }
            };
            _dbContext.SystemAlerts.AddRange(alerts);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await SeedUsersIfAvailableAsync(now, cancellationToken);
        _logger.LogInformation("Demo data seeding completed successfully.");
    }

    private async Task SeedUsersIfAvailableAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hasUsers = await _dbContext.Set<User>().AnyAsync(cancellationToken);
        if (hasUsers) return;

        var demoUsers = new List<User>
        {
            new() { Name = "Jeremy Tomaselly", Email = "jeremy@echoconsole.local", UserName = "jeremy@echoconsole.local", Alias = "jeremy_dev", AvatarKey = "avatar-01", Theme = "cyan", Role = UserRole.Admin, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-20) },
            new() { Name = "Julio Mera", Email = "julio@echoconsole.local", UserName = "julio@echoconsole.local", Alias = "julio_lead", AvatarKey = "avatar-02", Theme = "purple", Role = UserRole.Admin, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-19) },
            new() { Name = "Samuel Cobo", Email = "samuel@echoconsole.local", UserName = "samuel@echoconsole.local", Alias = "samuel_mod", AvatarKey = "avatar-03", Theme = "green", Role = UserRole.Moderator, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-17) },
            new() { Name = "Povea", Email = "povea@echoconsole.local", UserName = "povea@echoconsole.local", Alias = "povea", AvatarKey = "avatar-04", Theme = "cyan", Role = UserRole.Viewer, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-15) },
            new() { Name = "Armas", Email = "armas@echoconsole.local", UserName = "armas@echoconsole.local", Alias = "armas", AvatarKey = "avatar-05", Theme = "cyan", Role = UserRole.Viewer, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-14) }
        };

        _dbContext.Set<User>().AddRange(demoUsers);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}