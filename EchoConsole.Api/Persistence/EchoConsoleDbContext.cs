using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Persistence;

public sealed class EchoConsoleDbContext : IdentityUserContext<User, int>
{
    public EchoConsoleDbContext(DbContextOptions<EchoConsoleDbContext> options)
        : base(options)
    {
    }

    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<GameSessionEvent> GameSessionEvents => Set<GameSessionEvent>();
    public DbSet<GameBuild> GameBuilds => Set<GameBuild>();
    public DbSet<SystemAlert> SystemAlerts => Set<SystemAlert>();
    public DbSet<AlertTypeDefinition> AlertTypeDefinitions => Set<AlertTypeDefinition>();
    public DbSet<AlertDiscordOutboxMessage> AlertDiscordOutboxMessages =>
        Set<AlertDiscordOutboxMessage>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public override int SaveChanges()
    {
        EnqueueCriticalAlertOutboxMessages();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        EnqueueCriticalAlertOutboxMessages();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnqueueCriticalAlertOutboxMessages()
    {
        var criticalAlerts = ChangeTracker
            .Entries<SystemAlert>()
            .Where(entry =>
                entry.State == EntityState.Added &&
                (entry.Entity.Severity == AlertSeverity.Critical ||
                 entry.Entity.Severity == AlertSeverity.Fatal))
            .Select(entry => entry.Entity)
            .ToArray();

        if (criticalAlerts.Length == 0)
        {
            return;
        }

        var alreadyQueued = ChangeTracker
            .Entries<AlertDiscordOutboxMessage>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity.SystemAlert)
            .Where(alert => alert is not null)
            .ToHashSet(ReferenceEqualityComparer.Instance);

        var now = DateTimeOffset.UtcNow;

        foreach (var alert in criticalAlerts)
        {
            if (alreadyQueued.Contains(alert))
            {
                continue;
            }

            AlertDiscordOutboxMessages.Add(
                new AlertDiscordOutboxMessage
                {
                    SystemAlert = alert,
                    EnqueuedAtUtc = now,
                    NextAttemptUtc = now
                });
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(user =>
        {
            user.HasBaseType((Type?)null);

            user.ToTable("Users");

            user.HasIndex(x => x.Email).IsUnique();
            user.HasIndex(x => x.Alias).IsUnique();
            user.HasIndex(x => x.CreatedAtUtc);
            user.HasIndex(x => x.Status);

            user.Property(x => x.Name).HasMaxLength(100).IsRequired();
            user.Property(x => x.Email).HasMaxLength(256).IsRequired();
            user.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            user.Property(x => x.NormalizedEmail).HasMaxLength(256);
            user.Property(x => x.NormalizedUserName).HasMaxLength(256);

            user.Property(x => x.Alias).HasMaxLength(64).IsRequired();
            user.Property(x => x.AvatarKey).HasMaxLength(128).IsRequired();
            user.Property(x => x.Theme).HasMaxLength(32).IsRequired();
            user.Property(x => x.PreferredLanguage)
                .HasMaxLength(8)
                .HasDefaultValue("en")
                .IsRequired();
            user.Property(x => x.ProfileUpdatedAtUtc);

            user.Property(x => x.Role)
                .HasConversion(v => v.ToString(), v => Enum.Parse<UserRole>(v))
                .HasMaxLength(24)
                .IsRequired();

            user.Property(x => x.Status)
                .HasConversion(v => v.ToString(), v => Enum.Parse<UserStatus>(v))
                .HasMaxLength(24)
                .IsRequired();

            user.Property(x => x.CreatedAtUtc).IsRequired();

            user.HasMany(x => x.Installations)
                .WithOne(x => x.OwnerUser)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

        // --- UserSession ---
        modelBuilder.Entity<UserSession>(userSession =>
        {
            userSession.ToTable("UserSessions");

            userSession.HasKey(x => x.Id);

            userSession.HasIndex(x => x.SessionKeyHash)
                .IsUnique()
                .HasDatabaseName("IX_UserSessions_SessionKeyHash");

            userSession.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_UserSessions_UserId");

            userSession.HasIndex(x => x.ExpiresAtUtc)
                .HasDatabaseName("IX_UserSessions_ExpiresAtUtc");

            userSession.HasIndex(x => new
            {
                x.UserId,
                x.RevokedAtUtc,
                x.LastSeenAtUtc
            })
                .IsDescending(false, false, true)
                .HasDatabaseName(
                    "IX_UserSessions_UserId_RevokedAtUtc_LastSeenAtUtc");

            userSession.Property(x => x.SessionKeyHash)
                .HasMaxLength(64)
                .IsRequired();

            userSession.Property(x => x.UserAgent)
                .HasMaxLength(512)
                .IsRequired();

            userSession.Property(x => x.MaskedIpAddress)
                .HasMaxLength(64)
                .IsRequired();

            userSession.Property(x => x.CreatedAtUtc)
                .IsRequired();

            userSession.Property(x => x.LastSeenAtUtc)
                .IsRequired();

            userSession.Property(x => x.ExpiresAtUtc)
                .IsRequired();

            userSession.Property(x => x.RevokedReason)
                .HasMaxLength(128);

            userSession.HasOne(x => x.User)
                .WithMany(x => x.UserSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Installation ---
        modelBuilder.Entity<Installation>(installation =>
        {
            installation.HasKey(x => x.Id);
            installation.HasIndex(x => x.InstallationId).IsUnique();
            installation.HasIndex(x => x.DeviceName);
            installation.HasIndex(x => x.LastUpdateUtc);
            installation.HasIndex(x => x.OwnerUserId);
            installation.HasIndex(x => x.AdminAlias);
            installation.HasIndex(x => x.AdminStatus);

            installation.HasIndex(x => new { x.OwnerUserId, x.LastUpdateUtc })
                .IsDescending(false, true)
                .HasDatabaseName("IX_Installations_OwnerUserId_LastUpdateUtc");

            installation.Property(x => x.GameCode).HasMaxLength(64).IsRequired();
            installation.Property(x => x.BuildVersion).HasMaxLength(32).IsRequired();
            installation.Property(x => x.Platform).HasMaxLength(32).IsRequired();
            installation.Property(x => x.DeviceName).HasMaxLength(128).IsRequired();
            installation.Property(x => x.DeviceModel).HasMaxLength(128).IsRequired();
            installation.Property(x => x.OSVersion).HasMaxLength(128).IsRequired();
            installation.Property(x => x.Processor).HasMaxLength(128);
            installation.Property(x => x.Gpu).HasMaxLength(128);
            installation.Property(x => x.Status).HasMaxLength(24).IsRequired();
            installation.Property(x => x.AdminAlias).HasMaxLength(128);
            installation.Property(x => x.AdminStatus)
                .HasMaxLength(24)
                .HasDefaultValue("Active")
                .IsRequired();

            installation.HasOne(x => x.OwnerUser)
                .WithMany(x => x.Installations)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- GameSession ---
        modelBuilder.Entity<GameSession>(session =>
        {
            session.HasKey(x => x.Id);
            session.HasIndex(x => x.SessionId).IsUnique();
            session.HasIndex(x => x.LastHeartbeatUtc);
            session.HasIndex(x => x.BuildVersion)
                .HasDatabaseName("IX_GameSessions_BuildVersion");

            session.HasIndex(x => new { x.InstallationDbId, x.StartedAtUtc })
                .IsDescending(false, true)
                .HasDatabaseName("IX_GameSessions_InstallationDbId_StartedAtUtc");

            session.HasIndex(x => new { x.InstallationDbId, x.LastHeartbeatUtc })
                .IsDescending(false, true)
                .HasDatabaseName("IX_GameSessions_InstallationDbId_LastHeartbeatUtc");

            session.HasIndex(x => new { x.Status, x.EndedAtUtc, x.LastHeartbeatUtc })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_GameSessions_Status_EndedAtUtc_LastHeartbeatUtc");

            session.Property(x => x.SessionTokenHash).HasMaxLength(128).IsRequired();
            session.Property(x => x.BuildVersion).HasMaxLength(32).IsRequired();
            session.Property(x => x.CurrentScene).HasMaxLength(128).IsRequired();
            session.Property(x => x.CurrentGameState).HasMaxLength(64).IsRequired();
            session.Property(x => x.CurrentPhase).HasMaxLength(64);

            session.Property(x => x.Status)
                .HasConversion(v => v.ToString(), v => Enum.Parse<SessionStatus>(v))
                .HasMaxLength(24)
                .IsRequired();

            session.HasOne(x => x.Installation)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.InstallationDbId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- GameSessionEvent ---
        modelBuilder.Entity<GameSessionEvent>(sessionEvent =>
        {
            sessionEvent.ToTable("GameSessionEvents");

            sessionEvent.HasKey(x => x.Id);

            sessionEvent.HasIndex(x => new
            {
                x.GameSessionId,
                x.CreatedAtUtc
            })
                .IsDescending(false, true)
                .HasDatabaseName("IX_GameSessionEvents_GameSessionId_CreatedAtUtc");

            sessionEvent.HasIndex(x => x.CreatedAtUtc)
                .IsDescending(true)
                .HasDatabaseName("IX_GameSessionEvents_CreatedAtUtc");

            sessionEvent.HasIndex(x => new
            {
                x.EventType,
                x.CreatedAtUtc
            })
                .IsDescending(false, true)
                .HasDatabaseName("IX_GameSessionEvents_EventType_CreatedAtUtc");

            sessionEvent.HasIndex(x => x.EventType)
                .HasDatabaseName("IX_GameSessionEvents_EventType");

            sessionEvent.Property(x => x.EventType)
                .HasMaxLength(64)
                .IsRequired();

            sessionEvent.Property(x => x.Scene)
                .HasMaxLength(128);

            sessionEvent.Property(x => x.GameState)
                .HasMaxLength(64);

            sessionEvent.Property(x => x.Phase)
                .HasMaxLength(64);

            sessionEvent.Property(x => x.PayloadJson)
                .HasMaxLength(4000);

            sessionEvent.Property(x => x.CreatedAtUtc)
                .IsRequired();

            sessionEvent.HasOne(x => x.GameSession)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- GameBuild ---
        modelBuilder.Entity<GameBuild>(build =>
        {
            build.HasKey(x => x.Id);
            build.HasIndex(x => x.VersionNumber).IsUnique();
            build.HasIndex(x => x.ReleaseDateUtc);

            build.Property(x => x.VersionNumber).HasMaxLength(64).IsRequired();
            build.Property(x => x.ReleaseNotes).HasMaxLength(256);
            build.Property(x => x.EngineVersion).HasMaxLength(64).IsRequired();
        });

        // --- AlertTypeDefinition ---
        modelBuilder.Entity<AlertTypeDefinition>(alertType =>
        {
            alertType.ToTable("AlertTypeDefinitions");

            alertType.HasKey(x => x.Id);

            alertType.HasIndex(x => x.Code)
                .IsUnique()
                .HasDatabaseName("IX_AlertTypeDefinitions_Code");

            alertType.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_AlertTypeDefinitions_IsActive");

            alertType.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            alertType.Property(x => x.Code)
                .HasColumnType("nvarchar(64)")
                .HasMaxLength(64)
                .IsRequired();

            alertType.Property(x => x.Name)
                .HasColumnType("nvarchar(128)")
                .HasMaxLength(128)
                .IsRequired();

            alertType.Property(x => x.Description)
                .HasColumnType("nvarchar(500)")
                .HasMaxLength(500)
                .IsRequired();

            alertType.Property(x => x.DefaultSeverity)
                .HasConversion(
                    value => value.ToString(),
                    value => Enum.Parse<AlertSeverity>(value))
                .HasColumnType("nvarchar(24)")
                .HasMaxLength(24)
                .IsRequired();

            alertType.Property(x => x.IsActive)
                .HasColumnType("bit")
                .HasDefaultValue(true)
                .IsRequired();

            alertType.Property(x => x.CreatedAtUtc)
                .HasColumnType("datetimeoffset")
                .IsRequired();

            alertType.Property(x => x.UpdatedAtUtc)
                .HasColumnType("datetimeoffset")
                .IsRequired();
        });

        // --- SystemAlert ---
        modelBuilder.Entity<SystemAlert>(alert =>
        {
            alert.HasKey(x => x.Id);
            alert.HasIndex(x => x.CreatedAtUtc);
            alert.HasIndex(x => x.IsResolved);
            alert.HasIndex(x => x.ErrorTypeCode);
            alert.HasIndex(x => x.BuildVersion);
            alert.HasIndex(x => new
            {
                x.IsResolved,
                x.Severity,
                x.CreatedAtUtc
            })
                .IsDescending(false, false, true)
                .HasDatabaseName(
                    "IX_SystemAlerts_IsResolved_Severity_CreatedAtUtc");

            alert.Property(x => x.Severity)
                .HasConversion(
                    value => value.ToString(),
                    value => Enum.Parse<AlertSeverity>(value))
                .HasMaxLength(24)
                .IsRequired();

            alert.Property(x => x.ErrorTypeCode)
                .HasMaxLength(64)
                .HasDefaultValue("UNCLASSIFIED")
                .IsRequired();

            alert.Property(x => x.BuildVersion)
                .HasMaxLength(64);

            alert.Property(x => x.Message).HasMaxLength(500).IsRequired();
            alert.Property(x => x.Source).HasMaxLength(128).IsRequired();
            alert.Property(x => x.InstallationId).HasMaxLength(64);
            alert.Property(x => x.CreatedAtUtc).IsRequired();
            alert.Property(x => x.IsResolved).IsRequired();
            alert.Property(x => x.ResolvedAtUtc);
        });

        // --- AlertDiscordOutboxMessage ---
        modelBuilder.Entity<AlertDiscordOutboxMessage>(outbox =>
        {
            outbox.HasKey(x => x.Id);
            outbox.HasIndex(x => new
            {
                x.SentAtUtc,
                x.NextAttemptUtc
            });

            outbox.Property(x => x.EnqueuedAtUtc).IsRequired();
            outbox.Property(x => x.NextAttemptUtc).IsRequired();
            outbox.Property(x => x.LastError).HasMaxLength(1000);

            outbox.HasOne(x => x.SystemAlert)
                .WithMany(x => x.DiscordOutboxMessages)
                .HasForeignKey(x => x.SystemAlertId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}