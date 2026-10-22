using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Domain.Entities;

namespace SecurityCenterAI.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SecurityAnalysis> SecurityAnalyses => Set<SecurityAnalysis>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshCredential> RefreshCredentials => Set<RefreshCredential>();
    public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DevicePairingCode> DevicePairingCodes => Set<DevicePairingCode>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<SecurityRule> SecurityRules => Set<SecurityRule>();
    public DbSet<SecurityFinding> SecurityFindings => Set<SecurityFinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_usuario");
            entity.Property(x => x.Name).HasColumnName("nombre_completo").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasColumnName("correo_electronico").HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("hash_contrasena").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(x => x.EmailVerified).HasColumnName("correo_verificado");
            entity.Property(x => x.FailedLoginAttempts).HasColumnName("intentos_acceso_fallidos");
            entity.Property(x => x.LockoutEndUtc).HasColumnName("fecha_fin_bloqueo_utc");
            entity.Property(x => x.LastPasswordResetRequestedAtUtc).HasColumnName("fecha_ultima_solicitud_recuperacion_utc");
            entity.Property(x => x.LastVerificationSentAtUtc).HasColumnName("fecha_ultimo_envio_verificacion_utc");
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RefreshCredential>(entity =>
        {
            entity.ToTable("refresh_credentials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.UserSessionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccountActionToken>(entity =>
        {
            entity.ToTable("account_action_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Purpose });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SecurityAnalysis>(entity =>
        {
            entity.ToTable("security_analyses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceId).HasColumnName("id_dispositivo");
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.UserId, x.AnalyzedAtUtc });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Analyses)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        DeviceModelConfiguration.Configure(modelBuilder);
        AnalysisModelConfiguration.Configure(modelBuilder);
    }
}
