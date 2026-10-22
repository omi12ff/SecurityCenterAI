using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DevicesAndFindingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "id_dispositivo",
                table: "security_analyses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id_dispositivo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vinculacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_ultima_conexion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_revocacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plataforma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    version_agente = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    version_concurrencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id_dispositivo);
                    table.ForeignKey(
                        name: "FK_devices_users_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "users",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_rules",
                columns: table => new
                {
                    id_regla = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version_regla = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_rules", x => new { x.id_regla, x.version_regla });
                    table.CheckConstraint("CK_security_rules_version", "version_regla > 0");
                });

            migrationBuilder.CreateTable(
                name: "device_credentials",
                columns: table => new
                {
                    id_credencial_dispositivo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_dispositivo = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_credencial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_revocacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version_concurrencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.id_credencial_dispositivo);
                    table.ForeignKey(
                        name: "FK_device_credentials_devices_id_dispositivo",
                        column: x => x.id_dispositivo,
                        principalTable: "devices",
                        principalColumn: "id_dispositivo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_pairing_codes",
                columns: table => new
                {
                    id_codigo_vinculacion = table.Column<Guid>(type: "uuid", nullable: false),
                    id_dispositivo = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_consumo_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_revocacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version_concurrencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_pairing_codes", x => x.id_codigo_vinculacion);
                    table.ForeignKey(
                        name: "FK_device_pairing_codes_devices_id_dispositivo",
                        column: x => x.id_dispositivo,
                        principalTable: "devices",
                        principalColumn: "id_dispositivo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_findings",
                columns: table => new
                {
                    id_hallazgo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_analisis = table.Column<Guid>(type: "uuid", nullable: false),
                    id_regla = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version_regla = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    severidad = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    evidencia = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    recomendacion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    fecha_deteccion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_findings", x => x.id_hallazgo);
                    table.CheckConstraint("CK_security_findings_severidad", "severidad IN ('informational', 'low', 'medium', 'high', 'critical')");
                    table.ForeignKey(
                        name: "FK_security_findings_security_analyses_id_analisis",
                        column: x => x.id_analisis,
                        principalTable: "security_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_security_findings_security_rules_id_regla_version_regla",
                        columns: x => new { x.id_regla, x.version_regla },
                        principalTable: "security_rules",
                        principalColumns: new[] { "id_regla", "version_regla" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_security_analyses_id_dispositivo",
                table: "security_analyses",
                column: "id_dispositivo");

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_hash_credencial",
                table: "device_credentials",
                column: "hash_credencial",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_device_credentials_activa",
                table: "device_credentials",
                column: "id_dispositivo",
                unique: true,
                filter: "\"fecha_revocacion_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_device_pairing_codes_hash_codigo",
                table: "device_pairing_codes",
                column: "hash_codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_pairing_codes_id_dispositivo",
                table: "device_pairing_codes",
                column: "id_dispositivo");

            migrationBuilder.CreateIndex(
                name: "UX_devices_usuario_activo",
                table: "devices",
                column: "id_usuario",
                unique: true,
                filter: "\"fecha_revocacion_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_security_findings_id_analisis",
                table: "security_findings",
                column: "id_analisis");

            migrationBuilder.CreateIndex(
                name: "IX_security_findings_id_regla_version_regla",
                table: "security_findings",
                columns: new[] { "id_regla", "version_regla" });

            migrationBuilder.AddForeignKey(
                name: "FK_security_analyses_devices_id_dispositivo",
                table: "security_analyses",
                column: "id_dispositivo",
                principalTable: "devices",
                principalColumn: "id_dispositivo",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_security_analyses_devices_id_dispositivo",
                table: "security_analyses");

            migrationBuilder.DropTable(
                name: "device_credentials");

            migrationBuilder.DropTable(
                name: "device_pairing_codes");

            migrationBuilder.DropTable(
                name: "security_findings");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "security_rules");

            migrationBuilder.DropIndex(
                name: "IX_security_analyses_id_dispositivo",
                table: "security_analyses");

            migrationBuilder.DropColumn(
                name: "id_dispositivo",
                table: "security_analyses");
        }
    }
}
