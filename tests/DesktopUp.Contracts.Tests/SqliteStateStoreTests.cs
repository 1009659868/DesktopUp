using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class SqliteStateStoreTests
{
    [Fact]
    public void FreshStoreCreatesSchemaAndSecondInitializationIsIdempotent()
    {
        using var fixture = new StateStoreFixture();
        using var store = fixture.CreateStore();

        StateStoreInitializationResult first = store.Initialize();
        StateStoreInitializationResult second = store.Initialize();

        Assert.True(first.Succeeded);
        Assert.Equal(StateStoreInitializationStatus.Created, first.Status);
        Assert.Equal(18, first.Head?.SchemaVersion);
        Assert.Equal(0, first.Head?.Revision);
        Assert.Equal(StateStoreInitializationStatus.Ready, second.Status);
        Assert.Equal(first.Head?.Revision, second.Head?.Revision);
    }

    [Fact]
    public void CompareAndSwapCommitsConflictsAndReplaysIdempotently()
    {
        using var fixture = new StateStoreFixture();
        using var store = fixture.CreateStore();
        Assert.True(store.Initialize().Succeeded);

        StateCompareAndSwapResult committed = store.ExecuteCompareAndSwap(0, "op-1");
        StateCompareAndSwapResult conflict = store.ExecuteCompareAndSwap(0, "op-2");
        StateCompareAndSwapResult replay = store.ExecuteCompareAndSwap(0, "op-1");

        Assert.Equal(StateCompareAndSwapStatus.Committed, committed.Status);
        Assert.Equal(1, committed.CommittedRevision);
        Assert.Equal(StateCompareAndSwapStatus.Conflict, conflict.Status);
        Assert.Equal(1, conflict.CurrentRevision);
        Assert.Equal(StateCompareAndSwapStatus.Replayed, replay.Status);
        Assert.Equal(1, replay.CommittedRevision);
        Assert.Equal(1, store.ReadHead().Revision);
    }

    [Fact]
    public void CompareAndSwapRollsBackMutationAndOperationJournalTogether()
    {
        using var fixture = new StateStoreFixture();
        using var store = fixture.CreateStore();
        Assert.True(store.Initialize().Succeeded);

        Assert.Throws<FixtureMutationException>(() =>
            store.ExecuteCompareAndSwap(
                0,
                "op-rollback",
                static (connection, transaction, _) =>
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        "CREATE TABLE rollback_probe (value INTEGER) STRICT; " +
                        "INSERT INTO rollback_probe VALUES (1);";
                    command.ExecuteNonQuery();
                    throw new FixtureMutationException();
                }));

        Assert.Equal(0, store.ReadHead().Revision);
        StateCompareAndSwapResult retry = store.ExecuteCompareAndSwap(
            0,
            "op-rollback",
            static (connection, transaction, _) =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    "CREATE TABLE rollback_probe (value INTEGER) STRICT; " +
                    "INSERT INTO rollback_probe VALUES (1);";
                command.ExecuteNonQuery();
            });
        Assert.Equal(StateCompareAndSwapStatus.Committed, retry.Status);
    }

    [Fact]
    public void TwoStoreInstancesEnforceTheSameDatabaseRevision()
    {
        using var fixture = new StateStoreFixture();
        using var first = fixture.CreateStore();
        using var second = fixture.CreateStore();
        Assert.True(first.Initialize().Succeeded);
        Assert.True(second.Initialize().Succeeded);

        StateCompareAndSwapResult committed = first.ExecuteCompareAndSwap(0, "op-first");
        StateCompareAndSwapResult conflict = second.ExecuteCompareAndSwap(0, "op-second");

        Assert.Equal(StateCompareAndSwapStatus.Committed, committed.Status);
        Assert.Equal(StateCompareAndSwapStatus.Conflict, conflict.Status);
        Assert.Equal(1, conflict.CurrentRevision);
    }

    [Fact]
    public void OnlineBackupIsValidatedAndKeepsItsCommittedRevision()
    {
        using var fixture = new StateStoreFixture();
        using var store = fixture.CreateStore();
        Assert.True(store.Initialize().Succeeded);
        Assert.True(store.ExecuteCompareAndSwap(0, "op-before-backup").Succeeded);

        StateBackupResult backup = store.CreateBackup("test-snapshot");
        string backupPath = Path.Combine(fixture.Paths.Backups, backup.FileName!);
        StateBackupValidationResult validation = store.ValidateBackup(backupPath);

        Assert.True(backup.Succeeded);
        Assert.Equal(1, backup.Revision);
        Assert.True(validation.IsValid);
        Assert.Equal(18, validation.SchemaVersion);
        Assert.Equal(1, validation.Revision);
        Assert.Empty(Directory.EnumerateFiles(
            fixture.Paths.Backups,
            "*.tmp*",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void LegacyDatabaseGetsValidatedBackupBeforeMigration()
    {
        using var fixture = new StateStoreFixture();
        fixture.Paths.EnsureCreated();
        using (var connection = new SqliteConnection($"Data Source={fixture.DatabasePath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE legacy_fixture (value TEXT);";
            command.ExecuteNonQuery();
        }

        using var store = fixture.CreateStore();
        StateStoreInitializationResult result = store.Initialize();

        Assert.True(result.Succeeded);
        Assert.Equal(StateStoreInitializationStatus.Migrated, result.Status);
        Assert.Equal(0, result.PreviousSchemaVersion);
        Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.True(File.Exists(Path.Combine(
            fixture.Paths.Backups,
            result.PreMigrationBackupFileName!)));
        Assert.Equal(18, result.Head?.SchemaVersion);
    }

    [Fact]
    public void SchemaV1GetsSourceVersionBackupBeforeMigratingToCurrentSchema()
    {
        using var fixture = new StateStoreFixture();
        fixture.Paths.EnsureCreated();
        LegacyStateDatabase.Create(fixture.DatabasePath, 1);

        using var store = fixture.CreateStore();
        StateStoreInitializationResult result = store.Initialize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(StateStoreInitializationStatus.Migrated, result.Status);
        Assert.Equal(1, result.PreviousSchemaVersion);
        Assert.Equal(18, result.Head?.SchemaVersion);
        string backupPath = Path.Combine(
            fixture.Paths.Backups,
            result.PreMigrationBackupFileName!);
        Assert.True(File.Exists(backupPath));
        using var backup = new SqliteConnection(
            $"Data Source={backupPath};Mode=ReadOnly;Pooling=False");
        backup.Open();
        using SqliteCommand version = backup.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(1L, (long)version.ExecuteScalar()!);
        backup.Close();
        Assert.Empty(Directory.EnumerateFiles(
            fixture.Paths.Backups,
            "*.tmp*",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void CorruptDatabaseFailsWithoutBeingReplaced()
    {
        using var fixture = new StateStoreFixture();
        fixture.Paths.EnsureCreated();
        byte[] original = [0x44, 0x55, 0x50, 0x33, 0x00, 0x7F, 0x11];
        File.WriteAllBytes(fixture.DatabasePath, original);

        using var store = fixture.CreateStore();
        StateStoreInitializationResult result = store.Initialize();

        Assert.Equal(StateStoreInitializationStatus.Failed, result.Status);
        Assert.Equal(original, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Empty(Directory.EnumerateFiles(fixture.Paths.Backups));
    }

    [Fact]
    public void TamperedMigrationLedgerIsRejected()
    {
        using var fixture = new StateStoreFixture();
        using (var store = fixture.CreateStore())
        {
            Assert.True(store.Initialize().Succeeded);
        }

        using (var connection = new SqliteConnection($"Data Source={fixture.DatabasePath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "UPDATE schema_migrations SET checksum_sha256 = 'tampered' WHERE version = 1;";
            Assert.Equal(1, command.ExecuteNonQuery());
        }

        using var reopened = fixture.CreateStore();
        StateStoreInitializationResult result = reopened.Initialize();

        Assert.Equal(StateStoreInitializationStatus.Failed, result.Status);
        Assert.Contains("Migration ledger mismatch", result.Error, StringComparison.Ordinal);
    }

    private sealed class FixtureMutationException : Exception
    {
    }

    private sealed class StateStoreFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "DesktopUp.V3.StateStore.Tests",
            Guid.NewGuid().ToString("N"));

        public StateStoreFixture()
        {
            Paths = new UserDataPaths(_root);
        }

        public UserDataPaths Paths { get; }
        public string DatabasePath => Path.Combine(Paths.State, "desktopup.sqlite3");

        public SqliteStateStore CreateStore() => new(Paths);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
