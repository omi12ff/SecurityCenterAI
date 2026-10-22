using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserColumnsToSpanish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "users",
                newName: "hash_contrasena");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "users",
                newName: "nombre_completo");

            migrationBuilder.RenameColumn(
                name: "LockoutEndUtc",
                table: "users",
                newName: "fecha_fin_bloqueo_utc");

            migrationBuilder.RenameColumn(
                name: "LastVerificationSentAtUtc",
                table: "users",
                newName: "fecha_ultimo_envio_verificacion_utc");

            migrationBuilder.RenameColumn(
                name: "LastPasswordResetRequestedAtUtc",
                table: "users",
                newName: "fecha_ultima_solicitud_recuperacion_utc");

            migrationBuilder.RenameColumn(
                name: "FailedLoginAttempts",
                table: "users",
                newName: "intentos_acceso_fallidos");

            migrationBuilder.RenameColumn(
                name: "EmailVerified",
                table: "users",
                newName: "correo_verificado");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "users",
                newName: "correo_electronico");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "users",
                newName: "fecha_creacion_utc");

            migrationBuilder.RenameColumn(
                name: "ConcurrencyStamp",
                table: "users",
                newName: "version_concurrencia");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id_usuario");

            migrationBuilder.RenameIndex(
                name: "IX_users_Email",
                table: "users",
                newName: "IX_users_correo_electronico");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "version_concurrencia",
                table: "users",
                newName: "ConcurrencyStamp");

            migrationBuilder.RenameColumn(
                name: "nombre_completo",
                table: "users",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "intentos_acceso_fallidos",
                table: "users",
                newName: "FailedLoginAttempts");

            migrationBuilder.RenameColumn(
                name: "hash_contrasena",
                table: "users",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "fecha_ultimo_envio_verificacion_utc",
                table: "users",
                newName: "LastVerificationSentAtUtc");

            migrationBuilder.RenameColumn(
                name: "fecha_ultima_solicitud_recuperacion_utc",
                table: "users",
                newName: "LastPasswordResetRequestedAtUtc");

            migrationBuilder.RenameColumn(
                name: "fecha_fin_bloqueo_utc",
                table: "users",
                newName: "LockoutEndUtc");

            migrationBuilder.RenameColumn(
                name: "fecha_creacion_utc",
                table: "users",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "correo_verificado",
                table: "users",
                newName: "EmailVerified");

            migrationBuilder.RenameColumn(
                name: "correo_electronico",
                table: "users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id_usuario",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_users_correo_electronico",
                table: "users",
                newName: "IX_users_Email");
        }
    }
}
