using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateSecurityActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_activity",
                columns: table => new
                {
                    id_evento = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    fecha_evento_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_activity", x => x.id_evento);
                    table.CheckConstraint("CK_security_activity_tipo", "tipo_evento IN ('AccountRegistered','SessionStarted','SessionRevoked','AllSessionsRevoked','RefreshReplayBlocked','EmailVerified','PasswordReset','DeviceRegistered','PairingCodeIssued','DevicePaired','DeviceRevoked','AnalysisDeleted')");
                    table.ForeignKey(
                        name: "FK_security_activity_users_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "users",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_security_activity_id_usuario_fecha_evento_utc_id_evento",
                table: "security_activity",
                columns: new[] { "id_usuario", "fecha_evento_utc", "id_evento" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_activity");
        }
    }
}
