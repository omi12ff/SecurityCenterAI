using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountPasswordActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity");

            migrationBuilder.AddCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity",
                sql: "tipo_evento IN ('AccountRegistered','SessionStarted','SessionRevoked','AllSessionsRevoked','RefreshReplayBlocked','EmailVerified','PasswordReset','DeviceRegistered','PairingCodeIssued','DevicePaired','DeviceRevoked','AnalysisDeleted','PasswordChanged')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity");

            migrationBuilder.AddCheckConstraint(
                name: "CK_security_activity_tipo",
                table: "security_activity",
                sql: "tipo_evento IN ('AccountRegistered','SessionStarted','SessionRevoked','AllSessionsRevoked','RefreshReplayBlocked','EmailVerified','PasswordReset','DeviceRegistered','PairingCodeIssued','DevicePaired','DeviceRevoked','AnalysisDeleted')");
        }
    }
}
