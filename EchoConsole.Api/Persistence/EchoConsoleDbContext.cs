using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Persistence;

public sealed class EchoConsoleDbContext : DbContext
{
    public EchoConsoleDbContext(DbContextOptions<EchoConsoleDbContext> options)
        : base(options)
    {
    }

    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var installation = modelBuilder.Entity<Installation>();

        installation.HasKey(x => x.Id);
        installation.HasIndex(x => x.InstallationId).IsUnique();
        installation.HasIndex(x => x.DeviceName);
        installation.HasIndex(x => x.LastUpdateUtc);

        installation.Property(x => x.GameCode).HasMaxLength(64).IsRequired();
        installation.Property(x => x.BuildVersion).HasMaxLength(32).IsRequired();
        installation.Property(x => x.Platform).HasMaxLength(32).IsRequired();
        installation.Property(x => x.DeviceName).HasMaxLength(128).IsRequired();
        installation.Property(x => x.DeviceModel).HasMaxLength(128).IsRequired();
        installation.Property(x => x.OSVersion).HasMaxLength(128).IsRequired();
        installation.Property(x => x.Processor).HasMaxLength(128);
        installation.Property(x => x.Gpu).HasMaxLength(128);
        installation.Property(x => x.Status).HasMaxLength(24).IsRequired();

        var session = modelBuilder.Entity<GameSession>();

        session.HasKey(x => x.Id);
        session.HasIndex(x => x.SessionId).IsUnique();
        session.HasIndex(x => x.LastHeartbeatUtc);
        session.Property(x => x.SessionTokenHash).HasMaxLength(128).IsRequired();
        session.Property(x => x.BuildVersion).HasMaxLength(32).IsRequired();
        session.Property(x => x.CurrentScene).HasMaxLength(128).IsRequired();
        session.Property(x => x.CurrentGameState).HasMaxLength(64).IsRequired();
        session.Property(x => x.CurrentPhase).HasMaxLength(64);
        session.Property(x => x.Status)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<SessionStatus>(v))
            .HasMaxLength(24)
            .IsRequired();

        session.HasOne(x => x.Installation)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.InstallationDbId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}