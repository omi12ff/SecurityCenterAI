using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SecurityCenterAI.Infrastructure.Persistence;

#nullable disable

namespace SecurityCenterAI.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260727120000_EnforceSecurityAnalysisScoreRange")]
public partial class EnforceSecurityAnalysisScoreRange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "CK_security_analyses_Score",
            table: "security_analyses",
            sql: "\"Score\" >= 0 AND \"Score\" <= 100");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_security_analyses_Score",
            table: "security_analyses");
    }
}
