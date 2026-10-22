using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Domain.Entities;

namespace SecurityCenterAI.Infrastructure.Persistence;

internal static class DeviceModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_dispositivo");
            entity.Property(x => x.UserId).HasColumnName("id_usuario");
            entity.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(x => x.PairedAtUtc).HasColumnName("fecha_vinculacion_utc");
            entity.Property(x => x.LastSeenAtUtc).HasColumnName("fecha_ultima_conexion_utc");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("fecha_revocacion_utc");
            entity.Property(x => x.Platform).HasColumnName("plataforma").HasMaxLength(100);
            entity.Property(x => x.AgentVersion).HasColumnName("version_agente").HasMaxLength(64);
            entity.Property(x => x.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("\"fecha_revocacion_utc\" IS NULL").HasDatabaseName("UX_devices_usuario_activo");
        });
        builder.Entity<DevicePairingCode>(entity =>
        {
            entity.ToTable("device_pairing_codes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_codigo_vinculacion");
            entity.Property(x => x.DeviceId).HasColumnName("id_dispositivo");
            entity.Property(x => x.CodeHash).HasColumnName("hash_codigo").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("fecha_vencimiento_utc");
            entity.Property(x => x.ConsumedAtUtc).HasColumnName("fecha_consumo_utc");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("fecha_revocacion_utc");
            entity.Property(x => x.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.HasIndex(x => x.DeviceId);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("device_credentials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_credencial_dispositivo");
            entity.Property(x => x.DeviceId).HasColumnName("id_dispositivo");
            entity.Property(x => x.TokenHash).HasColumnName("hash_credencial").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("fecha_vencimiento_utc");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("fecha_revocacion_utc");
            entity.Property(x => x.ConcurrencyStamp).HasColumnName("version_concurrencia").IsConcurrencyToken();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.DeviceId).IsUnique().HasFilter("\"fecha_revocacion_utc\" IS NULL").HasDatabaseName("UX_device_credentials_activa");
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SecurityRule>(entity =>
        {
            entity.ToTable("security_rules", table => table.HasCheckConstraint("CK_security_rules_version", "version_regla > 0"));
            entity.HasKey(x => new { x.Id, x.Version });
            entity.Property(x => x.Id).HasColumnName("id_regla").HasMaxLength(64);
            entity.Property(x => x.Version).HasColumnName("version_regla");
            entity.Property(x => x.Title).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(4000).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("fecha_creacion_utc");
        });
        builder.Entity<SecurityFinding>(entity =>
        {
            entity.ToTable("security_findings", table => table.HasCheckConstraint("CK_security_findings_severidad",
                "severidad IN ('informational', 'low', 'medium', 'high', 'critical')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_hallazgo");
            entity.Property(x => x.SecurityAnalysisId).HasColumnName("id_analisis");
            entity.Property(x => x.RuleId).HasColumnName("id_regla").HasMaxLength(64).IsRequired();
            entity.Property(x => x.RuleVersion).HasColumnName("version_regla");
            entity.Property(x => x.Title).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Severity).HasColumnName("severidad").HasMaxLength(24).IsRequired();
            entity.Property(x => x.Evidence).HasColumnName("evidencia").HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Recommendation).HasColumnName("recomendacion").HasMaxLength(4000).IsRequired();
            entity.Property(x => x.DetectedAtUtc).HasColumnName("fecha_deteccion_utc");
            entity.HasOne(x => x.Analysis).WithMany(x => x.Findings).HasForeignKey(x => x.SecurityAnalysisId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Rule).WithMany().HasForeignKey(x => new { x.RuleId, x.RuleVersion }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
