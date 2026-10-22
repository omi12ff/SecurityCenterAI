using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;

namespace SecurityCenterAI.Infrastructure.Persistence;

internal static class AnalysisModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<SecurityAnalysis>(entity =>
        {
            entity.Property(x => x.Score).IsRequired(false);
            entity.Property(x => x.ReportId).HasColumnName("id_informe_agente");
            entity.Property(x => x.ReportHash).HasColumnName("hash_informe").HasMaxLength(64);
            entity.Property(x => x.SchemaVersion).HasColumnName("version_esquema");
            entity.Property(x => x.Source).HasColumnName("origen").HasMaxLength(32);
            entity.Property(x => x.AgentVersion).HasColumnName("version_agente").HasMaxLength(64);
            entity.Property(x => x.StartedAtUtc).HasColumnName("fecha_inicio_utc");
            entity.Property(x => x.CompletedAtUtc).HasColumnName("fecha_fin_utc");
            entity.Property(x => x.ScoringVersion).HasColumnName("version_puntaje");
            entity.Property(x => x.CoveragePercent).HasColumnName("porcentaje_cobertura");
            entity.Property(x => x.PassedCount).HasColumnName("cantidad_correctas");
            entity.Property(x => x.AttentionCount).HasColumnName("cantidad_revisar");
            entity.Property(x => x.UnknownCount).HasColumnName("cantidad_desconocidas");
            entity.Property(x => x.NotApplicableCount).HasColumnName("cantidad_no_aplicables");
            entity.HasIndex(x => new { x.DeviceId, x.ReportId }).IsUnique()
                .HasFilter("id_informe_agente IS NOT NULL").HasDatabaseName("UX_security_analyses_dispositivo_informe");
            entity.HasIndex(x => x.DeviceId);
        });
        builder.Entity<SecurityFinding>(entity =>
        {
            entity.Property(x => x.Status).HasColumnName("estado").HasMaxLength(24);
            entity.ToTable("security_findings", table => table.HasCheckConstraint("CK_security_findings_estado",
                "estado IS NULL OR estado IN ('passed', 'attention', 'unknown', 'notApplicable')"));
        });
        builder.Entity<SecurityRule>(entity =>
        {
            entity.Property(x => x.Severity).HasColumnName("severidad").HasMaxLength(24);
            entity.Property(x => x.Recommendation).HasColumnName("recomendacion").HasMaxLength(4000);
            entity.Property(x => x.Weight).HasColumnName("peso_puntaje");
            entity.HasData(WindowsRuleCatalog.Rules.Select(rule => new SecurityRule
            {
                Id = rule.Id, Version = WindowsRuleCatalog.Version, Title = rule.Title,
                Description = rule.Description, Severity = rule.Severity, Weight = rule.Weight,
                Recommendation = rule.Recommendation,
                CreatedAtUtc = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
            }));
        });
    }
}
