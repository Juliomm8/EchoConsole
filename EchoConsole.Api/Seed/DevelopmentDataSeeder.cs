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

            // CORRECCIÓN: Borrado fuertemente tipado para User
            if (_dbContext.Model.FindEntityType(typeof(User)) is not null)
                _dbContext.Set<User>().RemoveRange(_dbContext.Set<User>());

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var rng = new Random(20260513);

        var buildVersions = new[]
        {
            "0.1.0-dev", "0.2.0-alpha", "0.3.0-alpha",
            "0.4.0-beta", "0.5.0-beta", "1.0.0-rc1"
        };

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
                new()
                {
                    InstallationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    GameCode = "cosmic-diner", BuildVersion = "1.0.0-rc1", Platform = "WindowsPlayer",
                    DeviceName = "JEREMY-PC", DeviceModel = "Desktop", OSVersion = "Windows 11",
                    Processor = "Intel Core i7-12700H", Gpu = "NVIDIA RTX 3060", RamMb = 16384,
                    Status = "Active", FirstSeenUtc = now.AddDays(-12), LastUpdateUtc = now.AddMinutes(-1)
                },
                new()
                {
                    InstallationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    GameCode = "cosmic-diner", BuildVersion = "0.5.0-beta", Platform = "WindowsPlayer",
                    DeviceName = "ASUS-F15", DeviceModel = "Laptop", OSVersion = "Windows 11",
                    Processor = "Intel Core i5-11400H", Gpu = "NVIDIA GTX 1650", RamMb = 8192,
                    Status = "Active", FirstSeenUtc = now.AddDays(-9), LastUpdateUtc = now.AddMinutes(-3)
                },
                new()
                {
                    InstallationId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    GameCode = "cosmic-diner", BuildVersion = "0.4.0-beta", Platform = "WindowsPlayer",
                    DeviceName = "TEST-RIG-01", DeviceModel = "Desktop", OSVersion = "Windows 10",
                    Processor = "AMD Ryzen 5 5600X", Gpu = "AMD RX 6600", RamMb = 16384,
                    Status = "Active", FirstSeenUtc = now.AddDays(-20), LastUpdateUtc = now.AddHours(-2)
                },
                new()
                {
                    InstallationId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    GameCode = "cosmic-diner", BuildVersion = "0.3.0-alpha", Platform = "WindowsPlayer",
                    DeviceName = "LAB-PC-02", DeviceModel = "Desktop", OSVersion = "Windows 10",
                    Processor = "Intel Core i9-9900K", Gpu = "NVIDIA RTX 2070", RamMb = 32768,
                    Status = "Active", FirstSeenUtc = now.AddDays(-15), LastUpdateUtc = now.AddHours(-5)
                },
                new()
                {
                    InstallationId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    GameCode = "cosmic-diner", BuildVersion = "0.2.0-alpha", Platform = "WindowsPlayer",
                    DeviceName = "STREAM-BOX", DeviceModel = "Mini PC", OSVersion = "Windows 11",
                    Processor = "Intel Core i3-12100", Gpu = "Intel UHD Graphics", RamMb = 8192,
                    Status = "Active", FirstSeenUtc = now.AddDays(-7), LastUpdateUtc = now.AddHours(-8)
                }
            };

            _dbContext.Installations.AddRange(installations);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!_dbContext.GameSessions.Any())
        {
            var installations = await _dbContext.Installations.OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var sessions = new List<GameSession>();

            if (installations.Count >= 5)
            {
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[0].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[0].BuildVersion, CurrentScene = "OutdoorsScene", CurrentGameState = "Playing", CurrentPhase = "InvestigationStart", StartedAtUtc = now.AddMinutes(-28), LastHeartbeatUtc = now.AddSeconds(-10), Status = SessionStatus.Active });
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[1].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[1].BuildVersion, CurrentScene = "MainMenu", CurrentGameState = "Menu", CurrentPhase = "Boot", StartedAtUtc = now.AddMinutes(-6), LastHeartbeatUtc = now.AddSeconds(-18), Status = SessionStatus.Active });
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[2].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[2].BuildVersion, CurrentScene = "KitchenHall", CurrentGameState = "Paused", CurrentPhase = "PowerRestored", StartedAtUtc = now.AddMinutes(-43), LastHeartbeatUtc = now.AddMinutes(-20), EndedAtUtc = now.AddMinutes(-18), Status = SessionStatus.Ended });
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[3].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[3].BuildVersion, CurrentScene = "StorageRoom", CurrentGameState = "Playing", CurrentPhase = "LateGame", StartedAtUtc = now.AddMinutes(-55), LastHeartbeatUtc = now.AddMinutes(-3), Status = SessionStatus.Expired });
                sessions.Add(new GameSession { SessionId = Guid.NewGuid(), InstallationDbId = installations[4].Id, SessionTokenHash = Guid.NewGuid().ToString("N"), BuildVersion = installations[4].BuildVersion, CurrentScene = "Lobby", CurrentGameState = "Playing", CurrentPhase = "ReceptionArrival", StartedAtUtc = now.AddMinutes(-12), LastHeartbeatUtc = now.AddSeconds(-25), Status = SessionStatus.Active });
            }

            _dbContext.GameSessions.AddRange(sessions);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!_dbContext.SystemAlerts.Any())
        {
            var installations = await _dbContext.Installations.OrderBy(x => x.Id).ToListAsync(cancellationToken);
            string? installationId1 = installations.ElementAtOrDefault(0)?.InstallationId.ToString().ToUpperInvariant();
            string? installationId2 = installations.ElementAtOrDefault(1)?.InstallationId.ToString().ToUpperInvariant();

            var alerts = new List<SystemAlert>
            {
                new() { Severity = AlertSeverity.Info, Message = "Demo seed initialized successfully.", Source = "Server", InstallationId = null, CreatedAtUtc = now.AddHours(-10), IsResolved = true, ResolvedAtUtc = now.AddHours(-9) },
                new() { Severity = AlertSeverity.Warning, Message = "Missed heartbeat detected for one client session.", Source = "GameClient", InstallationId = installationId1, CreatedAtUtc = now.AddHours(-4), IsResolved = false },
                new() { Severity = AlertSeverity.Critical, Message = "Unexpected disconnect spike detected in current build.", Source = "Server", InstallationId = null, CreatedAtUtc = now.AddHours(-2), IsResolved = false },
                new() { Severity = AlertSeverity.Fatal, Message = "Simulated fatal crash report received from test device.", Source = "GameClient", InstallationId = installationId2, CreatedAtUtc = now.AddMinutes(-70), IsResolved = true, ResolvedAtUtc = now.AddMinutes(-45) },
                new() { Severity = AlertSeverity.Info, Message = "New installation registered successfully.", Source = "GameClient", InstallationId = installationId2, CreatedAtUtc = now.AddMinutes(-15), IsResolved = false }
            };

            _dbContext.SystemAlerts.AddRange(alerts);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await SeedUsersIfAvailableAsync(now, cancellationToken);
        _logger.LogInformation("Demo data seeding completed successfully.");
    }

    // CORRECCIÓN: Método totalmente fuertemente tipado
    private async Task SeedUsersIfAvailableAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hasUsers = await _dbContext.Set<User>().AnyAsync(cancellationToken);
        if (hasUsers) return;

        var demoUsers = new List<User>
        {
            new() { Name = "Jeremy Tomaselly", Email = "jeremy@echoconsole.local", Role = UserRole.Admin, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-20) },
            new() { Name = "Julio Mera", Email = "julio@echoconsole.local", Role = UserRole.Admin, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-19) },
            new() { Name = "Samuel Cobo", Email = "samuel@echoconsole.local", Role = UserRole.Moderator, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-17) },
            new() { Name = "Povea", Email = "povea@echoconsole.local", Role = UserRole.Viewer, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-15) },
            new() { Name = "Armas", Email = "armas@echoconsole.local", Role = UserRole.Viewer, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-14) },
            new() { Name = "QA Operator", Email = "qa.operator@echoconsole.local", Role = UserRole.Moderator, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-10) },
            new() { Name = "Telemetry Analyst", Email = "analyst@echoconsole.local", Role = UserRole.Viewer, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-9) },
            new() { Name = "System Observer", Email = "observer@echoconsole.local", Role = UserRole.Viewer, Status = UserStatus.Suspended, CreatedAtUtc = now.AddDays(-8) },
            new() { Name = "Build Manager", Email = "build.manager@echoconsole.local", Role = UserRole.Moderator, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-7) },
            new() { Name = "Report Reviewer", Email = "reviewer@echoconsole.local", Role = UserRole.Viewer, Status = UserStatus.Active, CreatedAtUtc = now.AddDays(-5) }
        };

        _dbContext.Set<User>().AddRange(demoUsers);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}