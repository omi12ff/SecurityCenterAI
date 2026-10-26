using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Services.Backups;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class RecoveryRehearsalTests
{
    [Fact]
    public void TargetMustBeTheExactOwnedRestoreNamespaceOnLocalPostgres()
    {
        var expected = "scai_restore_test_" + Guid.NewGuid().ToString("N");
        Assert.True(RestoredAccessReset.IsIsolatedTarget("127.0.0.1", 5432, expected, expected));
        Assert.False(RestoredAccessReset.IsIsolatedTarget("127.0.0.1", 5432, "security_center_ai", expected));
        Assert.False(RestoredAccessReset.IsIsolatedTarget("127.0.0.1", 5432, "scai_backup_fixture_" + Guid.NewGuid().ToString("N"), expected));
        Assert.False(RestoredAccessReset.IsIsolatedTarget("localhost", 5432, expected, expected));
        Assert.False(RestoredAccessReset.IsIsolatedTarget(null, 5432, expected, expected));
        Assert.False(RestoredAccessReset.IsIsolatedTarget("127.0.0.1", 5433, expected, expected));
        Assert.False(RestoredAccessReset.IsIsolatedTarget("127.0.0.1", 5432, expected + "x", expected + "x"));
        Assert.False(RestoredAccessReset.IsIsolatedTarget("127.0.0.1", 5432, expected, "scai_restore_test_" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void PolicyCoversTheCurrentModelAndRefusesUnknownMissingOrDuplicateTables()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var names = db.Model.GetEntityTypes().Select(item => item.GetTableName()!).Append("__EFMigrationsHistory").OrderBy(name => name).ToArray();
        var inventory = new BackupInventory(new string('0', 64), names.Select(name => new BackupTable(name, 0, new string('0', 64))).ToArray());
        RestoredAccessReset.RequireSupportedTables(inventory);
        Assert.Throws<InvalidDataException>(() => RestoredAccessReset.RequireSupportedTables(inventory with { Tables = inventory.Tables[..^1] }));
        Assert.Throws<InvalidDataException>(() => RestoredAccessReset.RequireSupportedTables(inventory with { Tables = [.. inventory.Tables, new("new_auth_store", 0, new string('0', 64))] }));
        Assert.Throws<InvalidDataException>(() => RestoredAccessReset.RequireSupportedTables(inventory with { Tables = [.. inventory.Tables[..^1], inventory.Tables[0]] }));
    }

    [Fact]
    public void ReportCannotBeMistakenForAnActiveRestoreOrReconciliationOfLaterDeletions()
    {
        var prepared = new RestoredAccessReport(DateTime.UtcNow, new(1, 2, 3, 4, 5, 6, 7, 1, 1, 1), 10, true, true);
        var report = new RecoveryRehearsalResult("rehearsed-in-isolation", "fixture.scaibackup", DateTime.UtcNow, 17, prepared);
        Assert.False(report.ActiveDatabaseReplaced); Assert.False(report.LaterDeletionsReconciled);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(new[] { "activeDatabaseReplaced", "file", "laterDeletionsReconciled", "preparation", "snapshotAtUtc", "status", "tablesVerified" },
            json.RootElement.EnumerateObject().Select(item => item.Name).OrderBy(name => name).ToArray());
        Assert.Equal(new[] { "access", "accessInvalidationChecked", "contentPreserved", "contentTablesChecked", "preparedAtUtc" },
            json.RootElement.GetProperty("preparation").EnumerateObject().Select(item => item.Name).OrderBy(name => name).ToArray());
    }
}
