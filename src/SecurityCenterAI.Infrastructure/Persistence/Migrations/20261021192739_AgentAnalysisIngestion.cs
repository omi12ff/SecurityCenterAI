using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentAnalysisIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "peso_puntaje",
                table: "security_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recomendacion",
                table: "security_rules",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severidad",
                table: "security_rules",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "security_findings",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "security_analyses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "cantidad_correctas",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_desconocidas",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_no_aplicables",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_revisar",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_fin_utc",
                table: "security_analyses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_inicio_utc",
                table: "security_analyses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hash_informe",
                table: "security_analyses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id_informe_agente",
                table: "security_analyses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen",
                table: "security_analyses",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "porcentaje_cobertura",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "version_agente",
                table: "security_analyses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_esquema",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_puntaje",
                table: "security_analyses",
                type: "integer",
                nullable: true);

            migrationBuilder.InsertData(
                table: "security_rules",
                columns: new[] { "id_regla", "version_regla", "fecha_creacion_utc", "descripcion", "recomendacion", "severidad", "titulo", "peso_puntaje" },
                values: new object[,]
                {
                    { "WIN-BITLOCKER-SYSTEM", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Observa el cifrado completo y la protección activa del volumen del sistema.", "Revisá el cifrado del dispositivo o BitLocker y la disponibilidad de tu clave de recuperación. La compatibilidad depende de la edición y del hardware; el agente no cifra ni descifra discos.", "medium", "Protección BitLocker del volumen del sistema", 10 },
                    { "WIN-DEFENDER-ANTIVIRUS", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Observa el estado declarado del antivirus y del servicio antimalware de Microsoft Defender.", "Revisá Seguridad de Windows y confirmá qué antivirus protege el equipo. Defender puede estar desactivado por otro producto o por una política; esta regla no evalúa otros antivirus.", "high", "Antivirus de Microsoft Defender", 15 },
                    { "WIN-DEFENDER-REALTIME", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Observa la protección en tiempo real de Microsoft Defender.", "Revisá la protección en tiempo real en Seguridad de Windows. Si usás otro antivirus, comprobá su estado; esta regla solo observa Defender.", "high", "Protección en tiempo real de Defender", 15 },
                    { "WIN-DEFENDER-SIGNATURES", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Compara la fecha informada de las firmas con un umbral de siete días; no busca la última versión disponible.", "Revisá las actualizaciones de inteligencia de seguridad de Defender. El umbral de esta regla es siete días y no verifica si existe una versión más nueva.", "medium", "Antigüedad de las firmas de Defender", 10 },
                    { "WIN-FIREWALL-PROFILES", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Observa si los perfiles Dominio, Privado y Público del firewall están habilitados.", "Revisá los perfiles Dominio, Privado y Público del firewall de Windows. En equipos administrados, consultá al responsable antes de cambiar políticas.", "high", "Perfiles del firewall de Windows", 15 },
                    { "WIN-SECURE-BOOT", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Consulta el estado de arranque seguro informado por el firmware.", "Revisá la compatibilidad con arranque seguro y la guía del fabricante. Conservá las claves de recuperación antes de modificar el firmware; el agente no realiza cambios.", "medium", "Arranque seguro", 10 },
                    { "WIN-SMB1-SERVER", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Consulta si SMB1 está deshabilitado en el servidor; no evalúa el cliente ni la exposición de puertos.", "Revisá si alguna dependencia antigua requiere SMB1 y planificá su retiro. Esta regla solo consulta el componente servidor; no evalúa el cliente SMB1 ni puertos expuestos.", "high", "SMB versión 1 del servidor", 5 },
                    { "WIN-TPM-READY", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Consulta la presencia y preparación del TPM sin recopilar identificadores ni claves.", "Revisá el procesador de seguridad en Seguridad de Windows. No borres ni reinicialices el TPM; esas acciones pueden afectar las claves de cifrado.", "medium", "TPM presente y preparado", 5 },
                    { "WIN-UAC-ENABLED", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Lee EnableLUA; no evalúa el nivel de aviso ni reinicios pendientes.", "Revisá Control de cuentas de usuario y las políticas del equipo. Esta regla solo lee EnableLUA; no evalúa el nivel de aviso ni confirma cambios pendientes de reinicio.", "medium", "Configuración de Control de cuentas de usuario", 10 },
                    { "WIN-UPDATE-SERVICE", 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Observa el modo de inicio del servicio; no determina si faltan parches.", "Revisá Windows Update y sus políticas. El inicio manual puede ser normal. Esta regla observa el servicio y no determina si faltan parches o si el sistema está actualizado.", "medium", "Disponibilidad del servicio Windows Update", 5 }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_security_findings_estado",
                table: "security_findings",
                sql: "estado IS NULL OR estado IN ('passed', 'attention', 'unknown', 'notApplicable')");

            migrationBuilder.CreateIndex(
                name: "UX_security_analyses_dispositivo_informe",
                table: "security_analyses",
                columns: new[] { "id_dispositivo", "id_informe_agente" },
                unique: true,
                filter: "id_informe_agente IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_security_findings_estado",
                table: "security_findings");

            migrationBuilder.DropIndex(
                name: "UX_security_analyses_dispositivo_informe",
                table: "security_analyses");

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-BITLOCKER-SYSTEM", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-DEFENDER-ANTIVIRUS", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-DEFENDER-REALTIME", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-DEFENDER-SIGNATURES", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-FIREWALL-PROFILES", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-SECURE-BOOT", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-SMB1-SERVER", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-TPM-READY", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-UAC-ENABLED", 1 });

            migrationBuilder.DeleteData(
                table: "security_rules",
                keyColumns: new[] { "id_regla", "version_regla" },
                keyValues: new object[] { "WIN-UPDATE-SERVICE", 1 });

            migrationBuilder.DropColumn(
                name: "peso_puntaje",
                table: "security_rules");

            migrationBuilder.DropColumn(
                name: "recomendacion",
                table: "security_rules");

            migrationBuilder.DropColumn(
                name: "severidad",
                table: "security_rules");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "security_findings");

            migrationBuilder.DropColumn(
                name: "cantidad_correctas",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "cantidad_desconocidas",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "cantidad_no_aplicables",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "cantidad_revisar",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "fecha_fin_utc",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "fecha_inicio_utc",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "hash_informe",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "id_informe_agente",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "origen",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "porcentaje_cobertura",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "version_agente",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "version_esquema",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "version_puntaje",
                table: "security_analyses");

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "security_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

        }
    }
}
