using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Domain.Entities;

namespace SecurityCenterAI.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SecurityAnalysis> SecurityAnalyses => Set<SecurityAnalysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<SecurityAnalysis>(entity =>
        {
            entity.ToTable(
                "security_analyses",
                table => table.HasCheckConstraint(
                    "CK_security_analyses_Score",
                    "\"Score\" >= 0 AND \"Score\" <= 100"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Score).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.AnalyzedAtUtc });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Analyses)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
