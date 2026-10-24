using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkMessageDailyUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "link_message_usage",
                columns: table => new
                {
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    consultas_realizadas = table.Column<int>(type: "integer", nullable: false),
                    version_concurrencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_message_usage", x => x.id_usuario);
                    table.CheckConstraint("CK_link_message_usage_consultas", "consultas_realizadas >= 0");
                    table.ForeignKey(
                        name: "FK_link_message_usage_users_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "users",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM link_message_usage) THEN
                        RAISE EXCEPTION 'Usage counters require a retention and quota plan before downgrade';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "link_message_usage");
        }
    }
}
