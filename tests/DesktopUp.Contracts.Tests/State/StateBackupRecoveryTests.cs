using DesktopUp.Application;
using System.Security.Cryptography;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class StateBackupRecoveryTests
{
    [Fact]
    public void Public_backup_validation_keeps_the_new_snapshot_standalone_and_restorable()
    {
        using var fixture = new Fixture();
        byte[] original = File.ReadAllBytes(fixture.Backup);
        using (var store = new SqliteStateStore(fixture.Paths))
        {
            Assert.True(store.Initialize().Succeeded);
            for (int i = 0; i < 3; i++) Assert.True(store.ValidateBackup(fixture.Backup).IsValid);
        }
        Assert.False(File.Exists(fixture.Backup + "-wal"), "Read-only validation must not leave a WAL beside an explicit backup.");
        Assert.False(File.Exists(fixture.Backup + "-shm"), "Read-only validation must not leave shared-memory state.");
        Assert.Equal(original, File.ReadAllBytes(fixture.Backup));
        var result = fixture.Recovery().Restore(Path.GetFileName(fixture.Backup));
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void RestorePreservesOriginalsAndBackupAndInstallsVerifiedRevision()
    {
        using var fixture = new Fixture();
        byte[] original = File.ReadAllBytes(fixture.Database);
        byte[] backup = File.ReadAllBytes(fixture.Backup);
        var recovery = fixture.Recovery();
        StateBackupCandidate candidate = Assert.Single(recovery.ListCandidates());
        Assert.True(candidate.IsValid, candidate.Error);
        Assert.Equal(1, candidate.Revision);
        StateBackupRecoveryResult result = recovery.Restore(Path.GetFileName(fixture.Backup));
        Assert.True(result.Succeeded, result.Error);
        Assert.False(recovery.HasPendingRecovery);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(result.ArchivePath!, "original", "desktopup.sqlite3")));
        Assert.Equal(backup, File.ReadAllBytes(fixture.Backup));
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.True(store.Initialize().Succeeded);
        Assert.Equal(1, store.ReadHead().Revision);
    }

    [Fact]
    public void SidecarsAreArchivedExactlyAndNeverAppliedToRestoredDatabase()
    {
        using var fixture = new Fixture();
        byte[] wal = "original-wal-evidence"u8.ToArray(), shm = "original-shm-evidence"u8.ToArray();
        File.WriteAllBytes(fixture.Database + "-wal", wal);
        File.WriteAllBytes(fixture.Database + "-shm", shm);
        StateBackupRecoveryResult result = fixture.Recovery().Restore(Path.GetFileName(fixture.Backup));
        Assert.True(result.Succeeded, result.Error);
        Assert.False(File.Exists(fixture.Database + "-wal"));
        Assert.False(File.Exists(fixture.Database + "-shm"));
        Assert.Equal(wal, File.ReadAllBytes(Path.Combine(result.ArchivePath!, "original", "desktopup.sqlite3-wal")));
        Assert.Equal(shm, File.ReadAllBytes(Path.Combine(result.ArchivePath!, "original", "desktopup.sqlite3-shm")));
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.True(store.Initialize().Succeeded);
        Assert.Equal(1, store.ReadHead().Revision);
    }

    [Fact]
    public void LegacyBackupIsMigratedOnlyOnStageAndSourceRemainsByteIdentical()
    {
        using var fixture = new Fixture();
        string legacy = Path.Combine(fixture.Paths.Backups, "legacy-v7.sqlite3");
        LegacyStateDatabase.Create(legacy, 7);
        byte[] source = File.ReadAllBytes(legacy);
        StateBackupRecoveryResult result = fixture.Recovery().Restore(Path.GetFileName(legacy));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(source, File.ReadAllBytes(legacy));
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.True(store.Initialize().Succeeded);
        Assert.Equal(store.LatestSchemaVersion, store.ReadHead().SchemaVersion);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("future")]
    [InlineData("wrong-application")]
    [InlineData("bad-checksum")]
    [InlineData("wal-dependent")]
    public void InvalidBackupCannotChangeOriginalState(string kind)
    {
        using var fixture = new Fixture();
        string candidate = Path.Combine(fixture.Paths.Backups, kind + ".sqlite3");
        File.Copy(fixture.Backup, candidate);
        if (kind == "corrupt") File.WriteAllText(candidate, "not sqlite");
        else if (kind == "wal-dependent") File.WriteAllText(candidate + "-wal", "sidecar");
        else
        {
            using var connection = new SqliteConnection($"Data Source={candidate};Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = kind switch
            {
                "future" => "PRAGMA user_version=9999;",
                "wrong-application" => "PRAGMA application_id=42;",
                _ => "UPDATE schema_migrations SET checksum_sha256='bad' WHERE version=1;"
            };
            command.ExecuteNonQuery();
        }
        byte[] before = File.ReadAllBytes(fixture.Database);
        byte[] source = File.ReadAllBytes(candidate);
        var recovery = fixture.Recovery();
        Assert.False(recovery.Restore(Path.GetFileName(candidate)).Succeeded);
        Assert.False(recovery.HasPendingRecovery);
        Assert.Equal(before, File.ReadAllBytes(fixture.Database));
        Assert.Equal(source, File.ReadAllBytes(candidate));
    }

    [Theory]
    [InlineData("../outside.sqlite3")]
    [InlineData(@"..\outside.sqlite3")]
    [InlineData(@"C:\outside.sqlite3")]
    [InlineData("backup.sqlite3:stream")]
    public void OnlyNamesInsideThisProfileBackupsAreAccepted(string name)
    {
        using var fixture = new Fixture();
        byte[] original = File.ReadAllBytes(fixture.Database);
        Assert.False(fixture.Recovery().Restore(name).Succeeded);
        Assert.Equal(original, File.ReadAllBytes(fixture.Database));
    }

    [Fact]
    public void OfflineLeaseAndClosedConnectionsAreRequired()
    {
        using var fixture = new Fixture();
        Assert.False(new StateBackupRecovery(fixture.Paths, () => false).Restore(Path.GetFileName(fixture.Backup)).Succeeded);
        using var lease = new FileStream(fixture.Database, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.False(fixture.Recovery().Restore(Path.GetFileName(fixture.Backup)).Succeeded);
        Assert.False(fixture.Recovery().HasPendingRecovery);
    }

    [Theory]
    [InlineData("recovery-after-validation", false)]
    [InlineData("recovery-after-journal", true)]
    [InlineData("recovery-after-sidecars", true)]
    [InlineData("recovery-after-replace", true)]
    public void InterruptedInstallationPreservesOriginalsAndExplicitRollbackIsRepeatable(string fault, bool pending)
    {
        using var fixture = new Fixture();
        byte[] original = File.ReadAllBytes(fixture.Database);
        byte[] wal = "original-wal"u8.ToArray(), shm = "original-shm"u8.ToArray();
        File.WriteAllBytes(fixture.Database + "-wal", wal);
        File.WriteAllBytes(fixture.Database + "-shm", shm);
        var broken = fixture.Recovery(stage => { if (stage == fault) throw new IOException("power loss"); });
        StateBackupRecoveryResult failed = broken.Restore(Path.GetFileName(fixture.Backup));
        Assert.False(failed.Succeeded);
        Assert.Equal(pending, broken.HasPendingRecovery);
        if (pending)
        {
            Assert.False(fixture.Recovery().Restore(Path.GetFileName(fixture.Backup)).Succeeded);
            StateBackupRecoveryResult rollback = fixture.Recovery().RecoverInterruptedRestore();
            Assert.True(rollback.Succeeded, rollback.Error);
            Assert.False(fixture.Recovery().HasPendingRecovery);
        }
        Assert.Equal(original, File.ReadAllBytes(fixture.Database));
        Assert.Equal(wal, File.ReadAllBytes(fixture.Database + "-wal"));
        Assert.Equal(shm, File.ReadAllBytes(fixture.Database + "-shm"));
        Assert.True(fixture.Recovery().RecoverInterruptedRestore().Succeeded);
    }

    [Fact]
    public void InterruptedRollbackCanBeSafelyRunAgain()
    {
        using var fixture = new Fixture();
        byte[] original = File.ReadAllBytes(fixture.Database);
        var failed = fixture.Recovery(stage => { if (stage == "recovery-after-replace") throw new IOException("stop"); });
        Assert.False(failed.Restore(Path.GetFileName(fixture.Backup)).Succeeded);
        var brokenRollback = fixture.Recovery(stage => { if (stage == "recovery-rollback-file") throw new IOException("again"); });
        Assert.False(brokenRollback.RecoverInterruptedRestore().Succeeded);
        Assert.True(brokenRollback.HasPendingRecovery);
        Assert.True(fixture.Recovery().RecoverInterruptedRestore().Succeeded);
        Assert.Equal(original, File.ReadAllBytes(fixture.Database));
    }

    [Fact]
    public void RepeatedExplicitRecoveryCreatesDistinctArchives()
    {
        using var fixture = new Fixture();
        StateBackupRecoveryResult first = fixture.Recovery().Restore(Path.GetFileName(fixture.Backup));
        StateBackupRecoveryResult second = fixture.Recovery().Restore(Path.GetFileName(fixture.Backup));
        Assert.True(first.Succeeded, first.Error);
        Assert.True(second.Succeeded, second.Error);
        Assert.NotEqual(first.ArchivePath, second.ArchivePath);
        Assert.True(Directory.Exists(first.ArchivePath));
        Assert.True(Directory.Exists(second.ArchivePath));
    }

    [Fact]
    public void DamagedLiveDatabaseIsArchivedWithoutOpeningOrRepairingIt()
    {
        using var fixture = new Fixture();
        byte[] corrupt = "unrecoverable live evidence"u8.ToArray();
        File.WriteAllBytes(fixture.Database, corrupt);
        StateBackupRecoveryResult result = fixture.Recovery().Restore(Path.GetFileName(fixture.Backup));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(corrupt, File.ReadAllBytes(Path.Combine(result.ArchivePath!, "original", "desktopup.sqlite3")));
    }

    [Fact]
    public void TamperedArchiveBlocksRollbackInsteadOfDestroyingRemainingEvidence()
    {
        using var fixture = new Fixture();
        var broken = fixture.Recovery(stage => { if (stage == "recovery-after-replace") throw new IOException("stop"); });
        StateBackupRecoveryResult failed = broken.Restore(Path.GetFileName(fixture.Backup));
        File.AppendAllText(Path.Combine(failed.ArchivePath!, "original", "desktopup.sqlite3"), "tampered");
        byte[] installed = File.ReadAllBytes(fixture.Database);
        Assert.False(fixture.Recovery().RecoverInterruptedRestore().Succeeded);
        Assert.True(fixture.Recovery().HasPendingRecovery);
        Assert.Equal(installed, File.ReadAllBytes(fixture.Database));
    }

    [Fact]
    public void CompletedInstallationPendingOnlyJournalArchiveIsFinalizedWithoutRollback()
    {
        using var fixture = new Fixture();
        var failed = fixture.Recovery(stage => { if (stage == "recovery-after-completed-journal") throw new IOException("stop"); });
        Assert.False(failed.Restore(Path.GetFileName(fixture.Backup)).Succeeded);
        Assert.True(failed.HasPendingRecovery);
        Assert.True(fixture.Recovery().RecoverInterruptedRestore().Succeeded);
        Assert.False(failed.HasPendingRecovery);
        using var store = new SqliteStateStore(fixture.Paths);
        Assert.True(store.Initialize().Succeeded);
        Assert.Equal(1, store.ReadHead().Revision);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("original")]
    [InlineData("phase")]
    public void TamperedJournalCannotEscapeProfileOrMutateLiveState(string field)
    {
        using var fixture = new Fixture();
        var failed = fixture.Recovery(stage => { if (stage == "recovery-after-journal") throw new IOException("stop"); });
        Assert.False(failed.Restore(Path.GetFileName(fixture.Backup)).Succeeded);
        string journalFile = Path.Combine(fixture.Paths.State, "recovery-pending.json");
        var journal = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalFile))!;
        if (field == "operation") journal["OperationId"] = "../../outside";
        if (field == "original") journal["Originals"]![0]!["Name"] = "../outside.sqlite3";
        if (field == "phase") journal["Phase"] = "unknown";
        File.WriteAllText(journalFile, journal.ToJsonString());
        byte[] original = File.ReadAllBytes(fixture.Database);
        Assert.False(fixture.Recovery().RecoverInterruptedRestore().Succeeded);
        Assert.True(fixture.Recovery().HasPendingRecovery);
        Assert.Equal(original, File.ReadAllBytes(fixture.Database));
    }


    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Paths = new UserDataPaths(Path.Combine(Path.GetTempPath(), "desktopup-recovery-" + Guid.NewGuid().ToString("N")));
            Paths.EnsureCreated();
            using var store = new SqliteStateStore(Paths);
            Assert.True(store.Initialize().Succeeded);
            Assert.True(store.ExecuteCompareAndSwap(0, "before-backup").Succeeded);
            var backup = store.CreateBackup("recovery-test");
            Assert.True(backup.Succeeded, backup.Error);
            Backup = Path.Combine(Paths.Backups, backup.FileName!);
            Assert.True(store.ExecuteCompareAndSwap(1, "after-backup").Succeeded);
        }
        public UserDataPaths Paths { get; }
        public string Database => Path.Combine(Paths.State, "desktopup.sqlite3");
        public string Backup { get; }
        public StateBackupRecovery Recovery(Action<string>? fault = null) => new(Paths, () => true, fault);
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(Paths.Root, recursive: true);
        }
    }
}
