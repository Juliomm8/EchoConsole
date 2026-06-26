using EchoConsole.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Api.Persistence.Cms;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<PatchNote> PatchNotes =>
        Set<PatchNote>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PatchNote>(patchNote =>
        {
            patchNote.ToTable("PatchNotes");

            patchNote.HasKey(x => x.Id);

            patchNote.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            patchNote.HasIndex(x => x.Version)
                .IsUnique();

            patchNote.HasIndex(x => x.CreatedAtUtc);

            patchNote.HasIndex(x => new
            {
                x.IsPublished,
                x.CreatedAtUtc
            });

            patchNote.Property(x => x.Version)
                .HasMaxLength(32)
                .IsRequired();

            patchNote.Property(x => x.Category)
                .HasMaxLength(32)
                .IsRequired();

            patchNote.Property(x => x.Tone)
                .HasMaxLength(16)
                .IsRequired();

            patchNote.Property(x => x.Title)
                .HasMaxLength(160)
                .IsRequired();

            patchNote.Property(x => x.Description)
                .HasMaxLength(4000)
                .IsRequired();

            patchNote.Property(x => x.CreatedAtUtc)
                .HasConversion(
                    value => value.UtcDateTime,
                    value => new DateTimeOffset(
                        DateTime.SpecifyKind(
                            value,
                            DateTimeKind.Utc)))
                .IsRequired();

            patchNote.Property(x => x.IsPublished)
                .IsRequired();
        });
    }
}
