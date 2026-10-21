using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentFrameMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V25_preserves_rows_children_and_backup_and_rolls_back_injected_failure(bool fail)
    {
        using var fixture = new Fixture();
        Assert.Equal("1", fixture.Query("SELECT count(*) FROM posters").Single());
        var before = fixture.Rows();
        var ledger = fixture.Query("SELECT checksum_sha256 FROM schema_migrations ORDER BY version");
        if (fail)
        {
            fixture.Execute("CREATE TRIGGER refuse_frame_migration BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'injected frame migration failure'); END;");
            Assert.Equal("1", fixture.Query("SELECT count(*) FROM sqlite_master WHERE name='refuse_frame_migration'").Single());
            Assert.Throws<SqliteException>(() => fixture.Execute("UPDATE posters SET title='must-not-change' WHERE poster_id='A';"));
        }
        using (var store = new SqliteStateStore(fixture.Paths))
        {
            var result = store.Initialize();
            if (fail)
            {
                Assert.False(result.Succeeded, JsonSerializer.Serialize(result) + "\nRows preserved: " + (before == fixture.Rows()));
                Assert.Equal("25", fixture.Query("PRAGMA user_version").Single());
                Assert.Equal(before, fixture.Rows());
                Assert.Empty(fixture.Query("SELECT name FROM pragma_table_info('posters') WHERE name LIKE 'next_%'"));
                Assert.Equal(ledger, fixture.Query("SELECT checksum_sha256 FROM schema_migrations ORDER BY version"));
                fixture.Execute("DROP TRIGGER refuse_frame_migration;");
                result = store.Initialize();
            }
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(26, result.Head!.SchemaVersion);
            Assert.Equal(before, fixture.Rows());
            Assert.Equal(ledger, fixture.Query("SELECT checksum_sha256 FROM schema_migrations WHERE version<=25 ORDER BY version"));
            string backup = Path.Combine(fixture.Paths.Backups, result.PreMigrationBackupFileName!);
            var validation = new StateBackupRecovery(fixture.Paths, () => true).ListCandidates()
                .Single(c => c.FileName == result.PreMigrationBackupFileName);
            Assert.True(validation.IsValid, validation.Error);
            Assert.Equal("25", fixture.Query("PRAGMA user_version", backup).Single());
            Assert.Equal(before, fixture.Rows(backup));
            Assert.Empty(fixture.Query("PRAGMA foreign_key_check"));
            fixture.Execute("UPDATE posters SET width_dip=96.5,expanded_height_dip=120.25 WHERE poster_id='A';");
            Assert.Throws<SqliteException>(() => fixture.Execute("UPDATE posters SET width_dip=95.99 WHERE poster_id='A';"));
            Assert.Throws<SqliteException>(() => fixture.Execute("UPDATE posters SET expanded_height_dip=95.99 WHERE poster_id='A';"));
        }
        using var reopened = new SqliteStateStore(fixture.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var poster = new SqlitePosterRepository(reopened).Read("A")!.Poster;
        Assert.Equal(96.5, poster.WidthDip);
        Assert.Equal(120.25, poster.ExpandedHeightDip);
        Assert.Equal("1", fixture.Query("SELECT count(*) FROM content_demo_archive").Single());
        Assert.Equal("1", fixture.Query("SELECT count(*) FROM spatial_participants").Single());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.FrameMigration.Tests", Guid.NewGuid().ToString("N"));
        public UserDataPaths Paths { get; }
        private string Database => Path.Combine(Paths.State, "desktopup.sqlite3");
        public Fixture()
        {
            Paths = new(_root); Paths.EnsureCreated();
            using var connection = Open();
            foreach (var migration in SqliteMigrationCatalog.Load(26).Take(25))
            {
                using var command = connection.CreateCommand();
                command.CommandText = migration.Sql; command.ExecuteNonQuery();
                if (migration.Version == 1)
                { command.CommandText = "INSERT INTO state_head VALUES(1,1,0,NULL,'2026-10-21T00:00:00Z');"; command.ExecuteNonQuery(); }
                command.CommandText = "INSERT INTO schema_migrations VALUES($v,$n,$h,'2026-10-21T00:00:00Z');";
                command.Parameters.AddWithValue("$v", migration.Version);
                command.Parameters.AddWithValue("$n", migration.Name);
                command.Parameters.AddWithValue("$h", migration.Checksum);
                command.ExecuteNonQuery();
            }
            Execute("""
                PRAGMA application_id=1146441779;
                PRAGMA user_version=25;
                UPDATE state_head SET schema_version=25,revision=1,last_operation_id='seed';
                INSERT INTO state_operations VALUES('seed',0,1,'2026-10-21T00:00:00Z');
                INSERT INTO posters(poster_id,title,subtitle,monitor_device_name,fallback_x_dip,fallback_y_dip,
                    width_dip,expanded_height_dip,horizontal_anchor,vertical_anchor,normalized_width,normalized_expanded_height,
                    is_locked,is_collapsed,auto_collapse_enabled,layout_kind,layout_parameters_json,template_key,
                    state_revision,created_utc,updated_utc,theme_instance_id)
                VALUES('A','Preserved','text','DISPLAY',15.5,30.25,300,600,0,0,.3,.6,0,0,0,
                    'adaptive-grid','{}','basic-box',1,'2026-10-21','2026-10-21','theme-A');
                INSERT INTO spatial_operations VALUES('seed','A',1,'active','{"keep":"proof"}',1);
                INSERT INTO spatial_participants VALUES('seed','A',1,'{"keep":"participant"}',NULL);
                INSERT INTO content_demo_archive VALUES('A','{"keep":"original"}',1);
                UPDATE poster_profile SET profile_initialized=1,catalog_version=1,state_revision=1;
                """);
        }
        private SqliteConnection Open(string? path = null)
        {
            var connection = new SqliteConnection($"Data Source={path ?? Database};Pooling=False;Foreign Keys=True");
            connection.Open(); return connection;
        }
        public void Execute(string sql) { using var c = Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
        public string[] Query(string sql, string? path = null)
        {
            using var c = Open(path); using var q = c.CreateCommand(); q.CommandText = sql;
            using var r = q.ExecuteReader(); var rows = new List<string>();
            while (r.Read()) rows.Add(Convert.ToString(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
            return rows.ToArray();
        }
        public string Rows(string? path = null)
        {
            // Compare every persisted business field by name; column order is not a data contract.
            var tables = Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('schema_migrations','state_head') ORDER BY name", path);
            var rows = new List<string>();
            using var c = Open(path);
            foreach (string table in tables)
            {
                using var q = c.CreateCommand(); q.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\" ORDER BY 1";
                using var r = q.ExecuteReader();
                while (r.Read())
                    rows.Add(table + JsonSerializer.Serialize(Enumerable.Range(0, r.FieldCount).OrderBy(r.GetName)
                        .ToDictionary(r.GetName, i => r.IsDBNull(i) ? null : r.GetValue(i))));
            }
            return JsonSerializer.Serialize(rows);
        }
        public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }
}
