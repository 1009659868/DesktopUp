using System.Reflection;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

// v1/v2 fingerprints captured from the previously verified v3-04-03/app-final
// assembly, not computed as expectations from the new implementation.
internal static class LegacyStateDatabase
{
    internal static readonly string[] FrozenHashes =
    [
        "AE9E436DEB9280A4DF7864A541EDC08A8964005ECCD19B47FCF47767FBFD3422",
        "75FFE962DA1729676D8183332FF8B3EFC58BE29E8D71DAC3F1F4E71FCED887C2",
        // Captured from the verified M01 app assembly before building M02.
        "DB57F3D22BEA1EB000AB1B4227F81956C0991E03DF1A56E1CEEB95CA6D53C194",
        // Extracted from git baseline a6ac6cf's scoped-command-receipts SQL before v5.
        "C0B674A2928ABF8B02407FD42B6885971FAF78EB936BAA95B4106C6F4E12237B",
        // Independently read from the retained M07 menu R1 Infrastructure assembly before v6.
        "655220756EA7D7718A07F5489E68F5096AD11693A36E9EF03F8823DC693F91B4",
        // Extracted independently from committed 53ebdb6 SQL before introducing v7.
        "D5FBCD000A17312F50BC7CDEFBC0B3516B0B2C8D9AB8C83C5B8CFE17F31B3B1D",
        // Independently read from the retained v7 tray candidate's database before adding v8.
        "65C3066942D70B2E9AA567C49768D49B996680F7A673ACB5957826FF01FBAEC6",
        // Read from the retained layout candidate's schema8 database before adding placement-pending.
        "F5C0306B35FE9142800FF574B56DA69E9F5F5BDD86971ABB2DAB956A8FA053A4"
    ];

    public static void Create(string path, int version)
    {
        var migrations = (Array)typeof(SqliteStateStore)
            .GetField("Migrations", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        for (int index = 0; index < version; index++)
        {
            object migration = migrations.GetValue(index)!;
            Type type = migration.GetType();
            string sql = (string)type.GetProperty("Sql")!.GetValue(migration)!;
            string hash = (string)type.GetProperty("Checksum")!.GetValue(migration)!;
            string name = (string)type.GetProperty("Name")!.GetValue(migration)!;
            Assert.Equal(FrozenHashes[index], hash);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
            if (index == 0)
            {
                command.CommandText = "INSERT INTO state_head VALUES(1, 1, 0, NULL, '2026-09-01T00:00:00Z');";
                command.ExecuteNonQuery();
            }
            command.CommandText = """
                INSERT INTO schema_migrations VALUES($version, $name, $hash, '2026-09-01T00:00:00Z');
                """;
            command.Parameters.AddWithValue("$version", index + 1);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$hash", hash);
            command.ExecuteNonQuery();
        }
        using SqliteCommand head = connection.CreateCommand();
        head.CommandText = $"""
            UPDATE state_head SET schema_version = {version};
            PRAGMA application_id=1146441779;
            PRAGMA user_version={version};
            """;
        head.ExecuteNonQuery();
    }
}
