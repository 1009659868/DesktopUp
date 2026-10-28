using System.Security.Cryptography;
using System.Text;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class MigrationFingerprintTests
{
    // Captured from retained PRE-FIX architecture product01 / package 20261016.101.
    private static readonly string[] ReleasedDigests =
    [
        "AE9E436DEB9280A4DF7864A541EDC08A8964005ECCD19B47FCF47767FBFD3422",
        "75FFE962DA1729676D8183332FF8B3EFC58BE29E8D71DAC3F1F4E71FCED887C2",
        "DB57F3D22BEA1EB000AB1B4227F81956C0991E03DF1A56E1CEEB95CA6D53C194",
        "C0B674A2928ABF8B02407FD42B6885971FAF78EB936BAA95B4106C6F4E12237B",
        "655220756EA7D7718A07F5489E68F5096AD11693A36E9EF03F8823DC693F91B4",
        "D5FBCD000A17312F50BC7CDEFBC0B3516B0B2C8D9AB8C83C5B8CFE17F31B3B1D",
        "65C3066942D70B2E9AA567C49768D49B996680F7A673ACB5957826FF01FBAEC6",
        "F5C0306B35FE9142800FF574B56DA69E9F5F5BDD86971ABB2DAB956A8FA053A4",
        "3DDD6008BD8A5F7F5E32C718B6EFE5D4AEFEB73D97B5E144B6491309EEBC45FD",
        "7195E8A7A5BE5AFEC6EBC30FBC1A6A6626FF47A8CF3E542AD48B8A186CF15E1C",
        "5CA1AE5E9B59FF85CBBFD62CB0B45CDD79CCE2E3F3A6FF346AD78466DAD834B7",
        "2A092EC95B2E844EF00B582947825A655550E2BF0C2FFD74F7FC07682EFA1958",
        "E94E6680BA64CCDB8A679EC088F5532DEFC4E7305E0B6066DE7F2C0A4CA4D57A",
        "9A99002BD028859AEC02D0A486C8BC3CC52D51E38DD66FE47F22E41F342EF013",
        "219B2F60131580008124812D62FFC16B7C69963BFE39BA2F67BEC55C860D4180",
        "E2F9C6ABF80B3B080ED3DA99593587ADC8CA3E0B0A448A31A43676AF395841B3",
        "4A732B79C822050A83F5C8A128BF42409F873A58846C5649BE05A93ADA6F4E8C",
        "AC2AD33F32236FD2F7430BEA819DBE8DED27D5DA20D8246C19A768BB8AC8E2B8",
        "D5C5E329F5450573F4A81D4745ACDC9C6DD1626E48BEF08EDFD9B094920C4BE5",
        "DACFEC80DC443934D69AEA3B34D20FB8ECE2B8035166BC99A53EECBBB533FC48",
        "02B70B1ADB0F64DEB3C43F0A63C4ACC3353CED550548B9A6725B9D206F2C5FA4",
        "A6B9349F557AB0848BBDBF82EA7E0B992AAF616A08D2D3F0C18AC070E845D2E7",
        "AEABA77CCBE89025CBD6BBAE2CDCC7DB8322292BA9DC4EB082F84D916FAFBF35",
        "85A29B250C1B68CA95082325EE3EDF667022FD390B5C9FB161B5407BCA788B98",
        "C77625B89B5B4521AC31E97FC51A698412C44202F268FC455F6D19AD81E9214C",
    ];

    [Fact]
    public void AllResourcesMatchTheirPinsAndKnownReleasedIdentities()
    {
        var migrations = SqliteMigrationCatalog.Load(27).Take(25).ToArray();
        foreach (var migration in migrations)
        {
            Assert.DoesNotContain("\r", migration.Sql);
            Assert.False(migration.Sql.EndsWith('\n'));
            Assert.Equal(migration.Checksum, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(migration.Sql))));
            Assert.True(migration.MatchesChecksum(ReleasedDigests[migration.Version - 1]));
        }
        // Read independently from the retained schema21 DB.
        Assert.True(migrations[12].MatchesChecksum("217BFBF8017E342B013D34777FECB68B54A3C28713A28293280D58346F19FA79"));
        Assert.True(migrations[16].MatchesChecksum("FB4ABBF3C0B6AF63CCAFD837A2F31C8CED50EEF17DC0EAC4537783559DFEB0C4"));
        // Do not accept every conceivable CRLF form, only the frozen, approved set.
        string unshipped = migrations[0].Sql.Replace("\n", "\r\n");
        Assert.False(migrations[0].MatchesChecksum(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(unshipped)))));
    }

    [Theory]
    [InlineData(21, true)]
    [InlineData(25, false)]
    public void HistoricalLedgerSurvivesUpgradeReopenAndBackup(int version, bool previewHistory)
    {
        using var fixture = new Fixture();
        fixture.CreateHistorical(version, previewHistory);
        string[] before = fixture.Ledger();
        using (var store = new SqliteStateStore(fixture.Paths))
        {
            var result = store.Initialize();
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(StateStoreInitializationStatus.Migrated, result.Status);
            Assert.Equal(before, fixture.Ledger().Take(version));
            if (version < 26)
            {
                var validation = new StateBackupRecovery(fixture.Paths, () => true).ListCandidates()
                    .Single(candidate => candidate.FileName == result.PreMigrationBackupFileName);
                Assert.True(validation.IsValid, validation.Error);
                Assert.Equal(version, validation.SchemaVersion);
            }
            var backup = store.CreateBackup("frozen-migration-review");
            Assert.True(backup.Succeeded, backup.Error);
            Assert.True(store.ValidateBackup(Path.Combine(fixture.Paths.Backups, backup.FileName!)).IsValid);
        }
        using var reopened = new SqliteStateStore(fixture.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(before, fixture.Ledger().Take(version));
    }

    [Fact]
    public void FreshDatabaseOnlyWritesCanonicalDigests()
    {
        using var fixture = new Fixture();
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.True(store.Initialize().Succeeded);
        Assert.Equal(SqliteMigrationCatalog.Load(27).Select(m => m.Checksum),
            fixture.Query("SELECT checksum_sha256 FROM schema_migrations ORDER BY version;"));
    }

    [Theory]
    [InlineData("UPDATE schema_migrations SET checksum_sha256='tampered' WHERE version=13;")]
    [InlineData("UPDATE schema_migrations SET name='wrong-name' WHERE version=13;")]
    [InlineData("UPDATE schema_migrations SET checksum_sha256=(SELECT checksum_sha256 FROM schema_migrations WHERE version=14) WHERE version=13;")]
    [InlineData("DROP TABLE content_open_facts;")]
    public void InvalidLedgerOrSchemaIsRejectedWithoutDatabaseRewrite(string change)
    {
        using var fixture = new Fixture();
        fixture.CreateHistorical(21, true);
        fixture.Execute(change);
        byte[] before = File.ReadAllBytes(fixture.Database);
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.False(store.Initialize().Succeeded);
        Assert.Equal(before, File.ReadAllBytes(fixture.Database));
    }

    [Fact]
    public void MutatedResourceIsNotNormalizedIntoAcceptedBytes()
    {
        var migration = SqliteMigrationCatalog.Load(27)[0];
        byte[] valid = Encoding.UTF8.GetBytes(migration.Sql);
        byte[][] invalid =
        [
            Encoding.UTF8.GetBytes(migration.Sql.Replace("\n", "\r\n")),
            [0xEF, 0xBB, 0xBF, ..valid],
            [..valid, 0x0A],
            [..valid, 0xFF],
            Encoding.UTF8.GetBytes(migration.Sql.Replace("state_head", "state_changed"))
        ];
        foreach (byte[] bytes in invalid)
            Assert.ThrowsAny<Exception>(() => SqliteMigration.FromBytes(1, migration.Name, bytes, migration.Checksum, []));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.MigrationIdentity.Tests", Guid.NewGuid().ToString("N"));
        internal Fixture() { Paths = new UserDataPaths(_root); Paths.EnsureCreated(); }
        internal UserDataPaths Paths { get; }
        internal string Database => Path.Combine(Paths.State, "desktopup.sqlite3");
        internal void CreateHistorical(int version, bool previewHistory)
        {
            using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
            connection.Open();
            foreach (var migration in SqliteMigrationCatalog.Load(27).Take(version))
            {
                using var command = connection.CreateCommand();
                command.CommandText = migration.Sql;
                command.ExecuteNonQuery();
                if (migration.Version == 1)
                {
                    command.CommandText = "INSERT INTO state_head VALUES(1,1,0,NULL,'2026-09-01T00:00:00Z');";
                    command.ExecuteNonQuery();
                }
                string hash = (previewHistory, migration.Version) switch
                {
                    (true, 13) => "217BFBF8017E342B013D34777FECB68B54A3C28713A28293280D58346F19FA79",
                    (true, 17) => "FB4ABBF3C0B6AF63CCAFD837A2F31C8CED50EEF17DC0EAC4537783559DFEB0C4",
                    _ => ReleasedDigests[migration.Version - 1]
                };
                command.CommandText = "INSERT INTO schema_migrations VALUES($version,$name,$hash,'2026-09-01T00:00:00Z');";
                command.Parameters.AddWithValue("$version", migration.Version);
                command.Parameters.AddWithValue("$name", migration.Name);
                command.Parameters.AddWithValue("$hash", hash);
                command.ExecuteNonQuery();
            }
            using var head = connection.CreateCommand();
            head.CommandText = $"UPDATE state_head SET schema_version={version}; PRAGMA user_version={version}; PRAGMA application_id=1146441779;";
            head.ExecuteNonQuery();
        }
        internal void Execute(string sql)
        {
            using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        internal string[] Ledger() => Query("SELECT version || '|' || name || '|' || checksum_sha256 || '|' || applied_utc FROM schema_migrations ORDER BY version;");
        internal string[] Query(string sql)
        {
            using var connection = new SqliteConnection($"Data Source={Database};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<string>();
            while (reader.Read()) rows.Add(reader.GetString(0));
            return rows.ToArray();
        }
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
