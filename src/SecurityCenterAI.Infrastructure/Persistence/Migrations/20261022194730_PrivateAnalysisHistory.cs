using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateAnalysisHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deleted_analysis_reports",
                columns: table => new
                {
                    id_dispositivo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_informe_agente = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_eliminacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deleted_analysis_reports", x => new { x.id_dispositivo, x.id_informe_agente });
                    table.ForeignKey(
                        name: "FK_deleted_analysis_reports_devices_id_dispositivo",
                        column: x => x.id_dispositivo,
                        principalTable: "devices",
                        principalColumn: "id_dispositivo",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deleted_analysis_reports");
        }
    }
}
