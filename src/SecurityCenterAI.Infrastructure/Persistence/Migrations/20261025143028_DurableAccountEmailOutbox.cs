using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableAccountEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_email_outbox",
                columns: table => new
                {
                    id_correo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    contenido_cifrado = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_expiracion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_proximo_intento_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    id_reserva = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_fin_reserva_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version_concurrencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_email_outbox", x => x.id_correo);
                    table.CheckConstraint("CK_account_email_outbox_intentos", "intentos >= 0 AND intentos <= 5");
                    table.CheckConstraint("CK_account_email_outbox_reserva", "(id_reserva IS NULL) = (fecha_fin_reserva_utc IS NULL)");
                    table.CheckConstraint("CK_account_email_outbox_vigencia", "fecha_expiracion_utc > fecha_creacion_utc");
                    table.ForeignKey(
                        name: "FK_account_email_outbox_users_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "users",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_email_outbox_fecha_expiracion_utc",
                table: "account_email_outbox",
                column: "fecha_expiracion_utc");

            migrationBuilder.CreateIndex(
                name: "IX_account_email_outbox_fecha_proximo_intento_utc_id_correo",
                table: "account_email_outbox",
                columns: new[] { "fecha_proximo_intento_utc", "id_correo" });

            migrationBuilder.CreateIndex(
                name: "IX_account_email_outbox_id_usuario",
                table: "account_email_outbox",
                column: "id_usuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE account_email_outbox IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM account_email_outbox) THEN
                    RAISE EXCEPTION 'No se puede retirar la cola mientras haya correos pendientes.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "account_email_outbox");
        }
    }
}
