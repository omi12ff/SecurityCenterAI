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
    public DbSet<EmailChangeRequest> EmailChangeRequests => Set<EmailChangeRequest>();
    public DbSet<LinkMessageUsage> LinkMessageUsage => Set<LinkMessageUsage>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DevicePairingCode> DevicePairingCodes => Set<DevicePairingCode>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<SecurityRule> SecurityRules => Set<SecurityRule>();
    public DbSet<SecurityFinding> SecurityFindings => Set<SecurityFinding>();
    public DbSet<AnalysisFeedback> AnalysisFeedback => Set<AnalysisFeedback>();
    public DbSet<DeletedAnalysisReport> DeletedAnalysisReports => Set<DeletedAnalysisReport>();
    public DbSet<SecurityActivity> SecurityActivities => Set<SecurityActivity>();
    public DbSet<AccountEmailOutbox> AccountEmailOutbox => Set<AccountEmailOutbox>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardSecurityActivity();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardSecurityActivity();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardSecurityActivity()
    {
        var removedOwners = ChangeTracker.Entries<User>().Where(entry => entry.State == EntityState.Deleted).Select(entry => entry.Entity.Id).ToHashSet();
        foreach (var entry in ChangeTracker.Entries<SecurityActivity>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted && !removedOwners.Contains(entry.Entity.UserId))
                throw new InvalidOperationException("La actividad de seguridad solo admite nuevas entradas o el borrado de su cuenta.");
            if (entry.State == EntityState.Added && (entry.Entity.UserId == Guid.Empty || !Enum.IsDefined(entry.Entity.Kind) || entry.Entity.OccurredAtUtc.Kind != DateTimeKind.Utc))
                throw new InvalidOperationException("Entrada de actividad de seguridad inválida.");
        }
    }

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

        modelBuilder.Entity<LinkMessageUsage>(entity =>
        {
            entity.ToTable("link_message_usage", table => table.HasCheckConstraint("CK_link_message_usage_consultas", "consultas_realizadas >= 0"));
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("id_usuario");
            entity.Property(item => item.DayUtc).HasColumnName("dia_utc");
            entity.Property(item => item.Checks).HasColumnName("consultas_realizadas");
            entity.Property(item => item.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountEmailOutbox>(entity =>
        {
            entity.ToTable("account_email_outbox", table =>
            {
                table.HasCheckConstraint("CK_account_email_outbox_intentos", "intentos >= 0 AND intentos <= 5");
                table.HasCheckConstraint("CK_account_email_outbox_vigencia", "fecha_expiracion_utc > fecha_creacion_utc");
                table.HasCheckConstraint("CK_account_email_outbox_reserva", "(id_reserva IS NULL) = (fecha_fin_reserva_utc IS NULL)");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id_correo");
            entity.Property(item => item.UserId).HasColumnName("id_usuario");
            entity.Property(item => item.ProtectedPayload).HasColumnName("contenido_cifrado").HasMaxLength(8192).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(item => item.ExpiresAtUtc).HasColumnName("fecha_expiracion_utc");
            entity.Property(item => item.NextAttemptAtUtc).HasColumnName("fecha_proximo_intento_utc");
            entity.Property(item => item.Attempts).HasColumnName("intentos");
            entity.Property(item => item.LeaseId).HasColumnName("id_reserva");
            entity.Property(item => item.LeaseExpiresAtUtc).HasColumnName("fecha_fin_reserva_utc");
            entity.Property(item => item.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
            entity.HasIndex(item => new { item.NextAttemptAtUtc, item.Id });
            entity.HasIndex(item => item.ExpiresAtUtc);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailChangeRequest>(entity =>
        {
            entity.ToTable("email_change_requests");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("id_usuario");
            entity.Property(x => x.Id).HasColumnName("id_solicitud");
            entity.Property(x => x.CurrentEmail).HasColumnName("correo_actual").HasMaxLength(320).IsRequired();
            entity.Property(x => x.NewEmail).HasColumnName("correo_nuevo").HasMaxLength(320).IsRequired();
            entity.Property(x => x.CurrentTokenHash).HasColumnName("hash_confirmacion_actual").HasMaxLength(64).IsRequired();
            entity.Property(x => x.NewTokenHash).HasColumnName("hash_confirmacion_nuevo").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("fecha_expiracion_utc");
            entity.Property(x => x.CurrentConfirmedAtUtc).HasColumnName("fecha_confirmacion_actual_utc");
            entity.Property(x => x.NewConfirmedAtUtc).HasColumnName("fecha_confirmacion_nuevo_utc");
            entity.Property(x => x.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
            entity.HasIndex(x => x.Id).IsUnique();
            entity.HasIndex(x => x.CurrentTokenHash).IsUnique();
            entity.HasIndex(x => x.NewTokenHash).IsUnique();
            entity.HasIndex(x => x.ExpiresAtUtc);
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
        modelBuilder.Entity<DeletedAnalysisReport>(entity =>
        {
            entity.ToTable("deleted_analysis_reports");
            entity.HasKey(x => new { x.DeviceId, x.ReportId });
            entity.Property(x => x.DeviceId).HasColumnName("id_dispositivo");
            entity.Property(x => x.ReportId).HasColumnName("id_informe_agente");
            entity.Property(x => x.DeletedAtUtc).HasColumnName("fecha_eliminacion_utc");
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AnalysisFeedback>(entity =>
        {
            entity.ToTable("analysis_feedback");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_comentario");
            entity.Property(x => x.UserId).HasColumnName("id_usuario");
            entity.Property(x => x.AnalysisId).HasColumnName("id_analisis");
            entity.Property(x => x.Rating).HasColumnName("valoracion").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Comment).HasColumnName("comentario").HasMaxLength(1000);
            entity.Property(x => x.ExplanationVersion).HasColumnName("version_guia");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("fecha_actualizacion_utc");
            entity.HasIndex(x => new { x.UserId, x.AnalysisId }).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Analysis).WithMany().HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SecurityActivity>(entity =>
        {
            var kinds = string.Join(",", Enum.GetNames<SecurityActivityKind>().Select(kind => "'" + kind + "'"));
            entity.ToTable("security_activity", table => table.HasCheckConstraint("CK_security_activity_tipo", $"tipo_evento IN ({kinds})"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id_evento");
            entity.Property(item => item.UserId).HasColumnName("id_usuario");
            entity.Property(item => item.Kind).HasColumnName("tipo_evento").HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.OccurredAtUtc).HasColumnName("fecha_evento_utc");
            entity.HasIndex(item => new { item.UserId, item.OccurredAtUtc, item.Id }).IsDescending(false, true, true);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_security_activity_users_id_usuario");
        });
    }
}
