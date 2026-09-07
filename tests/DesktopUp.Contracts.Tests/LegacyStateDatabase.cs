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
        "DB57F3D22BEA1EB000AB1B4227F81956C0991E03DF1A56E1CEEB95CA6D53C194"
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
