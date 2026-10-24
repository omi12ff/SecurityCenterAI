using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmedEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity");

            migrationBuilder.CreateTable(
                name: "email_change_requests",
                columns: table => new
                {
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_solicitud = table.Column<Guid>(type: "uuid", nullable: false),
                    correo_actual = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    correo_nuevo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    hash_confirmacion_actual = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hash_confirmacion_nuevo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_expiracion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_confirmacion_actual_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_confirmacion_nuevo_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version_concurrencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_change_requests", x => x.id_usuario);
                    table.ForeignKey(
                        name: "FK_email_change_requests_users_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "users",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity",
                sql: "tipo_evento IN ('AccountRegistered','SessionStarted','SessionRevoked','AllSessionsRevoked','RefreshReplayBlocked','EmailVerified','PasswordReset','DeviceRegistered','PairingCodeIssued','DevicePaired','DeviceRevoked','AnalysisDeleted','PasswordChanged','EmailChanged')");

            migrationBuilder.CreateIndex(
                name: "IX_email_change_requests_fecha_expiracion_utc",
                table: "email_change_requests",
                column: "fecha_expiracion_utc");

            migrationBuilder.CreateIndex(
                name: "IX_email_change_requests_hash_confirmacion_actual",
                table: "email_change_requests",
                column: "hash_confirmacion_actual",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_change_requests_hash_confirmacion_nuevo",
                table: "email_change_requests",
                column: "hash_confirmacion_nuevo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_change_requests_id_solicitud",
                table: "email_change_requests",
                column: "id_solicitud",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM email_change_requests) THEN
                        RAISE EXCEPTION 'Email change requests must be resolved before downgrade';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "email_change_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity");

            migrationBuilder.AddCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity",
                sql: "tipo_evento IN ('AccountRegistered','SessionStarted','SessionRevoked','AllSessionsRevoked','RefreshReplayBlocked','EmailVerified','PasswordReset','DeviceRegistered','PairingCodeIssued','DevicePaired','DeviceRevoked','AnalysisDeleted','PasswordChanged')");
        }
    }
}
