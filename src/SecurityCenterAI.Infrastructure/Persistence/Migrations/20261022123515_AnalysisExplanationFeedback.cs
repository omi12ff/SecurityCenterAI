using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalysisExplanationFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_feedback",
                columns: table => new
                {
                    id_comentario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_analisis = table.Column<Guid>(type: "uuid", nullable: false),
                    valoracion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version_guia = table.Column<int>(type: "integer", nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_feedback", x => x.id_comentario);
                    table.ForeignKey(
                        name: "FK_analysis_feedback_security_analyses_id_analisis",
                        column: x => x.id_analisis,
                        principalTable: "security_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analysis_feedback_users_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "users",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_feedback_id_analisis",
                table: "analysis_feedback",
                column: "id_analisis");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_feedback_id_usuario_id_analisis",
                table: "analysis_feedback",
                columns: new[] { "id_usuario", "id_analisis" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_feedback");
        }
    }
}
