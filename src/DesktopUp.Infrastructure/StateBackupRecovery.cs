using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using DesktopUp.Application;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record StateBackupCandidate(
    string FileName, DateTime LastWriteUtc, bool IsValid, int SchemaVersion, long? Revision, string? Error);

public sealed record StateBackupRecoveryResult(bool Succeeded, string? Error, string? ArchivePath);

/// <summary>
/// Explicit offline recovery only. The owner must hold the application single-instance lease,
/// dispose every state store/connection, and call this before initializing a new state store.
/// A durable pending journal blocks normal startup until explicitly resolved.
/// </summary>
public sealed class StateBackupRecovery
{
    private const string DatabaseName = "desktopup.sqlite3";
    private const string JournalName = "recovery-pending.json";
    private const long MaximumBackupBytes = 256L * 1024 * 1024;
    private static readonly string[] StateFiles = [DatabaseName, DatabaseName + "-wal", DatabaseName + "-shm"];
    private readonly IUserDataPaths _paths;
    private readonly Func<bool> _canRestoreOffline;
    private readonly Action<string>? _fault;
    private readonly object _gate = new();

    public StateBackupRecovery(IUserDataPaths paths, Func<bool> canRestoreOffline, Action<string>? fault = null)
    {
        _paths = paths;
        _canRestoreOffline = canRestoreOffline;
        _fault = fault;
    }

    public bool HasPendingRecovery => File.Exists(Path.Combine(_paths.State, JournalName));

    public IReadOnlyList<StateBackupCandidate> ListCandidates()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_paths.Backups)) return [];
            LocalDataFileSafety.Normalize(Path.Combine(_paths.Backups, ".validation-path"));
            var candidates = new List<StateBackupCandidate>();
            foreach (FileInfo file in new DirectoryInfo(_paths.Backups).EnumerateFiles("*.sqlite3")
                .OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.Name, StringComparer.Ordinal)
                .Take(100))
            {
                string validationRoot = Path.Combine(_paths.Temp, "recovery-validation-" + Guid.NewGuid().ToString("N"));
                try
                {
                    PreparedBackup prepared = PrepareBackup(file.Name, validationRoot);
                    candidates.Add(new(file.Name, file.LastWriteTimeUtc, true,
                        prepared.SourceSchemaVersion, prepared.Revision, null));
                }
                catch (Exception exception)
                {
                    candidates.Add(new(file.Name, file.LastWriteTimeUtc, false, 0, null, exception.Message));
                }
                finally
                {
                    // This directory contains only copies created by this validation, never a source backup.
                    if (Directory.Exists(validationRoot)) Directory.Delete(validationRoot, recursive: true);
                }
            }
            return candidates;
        }
    }

    public StateBackupRecoveryResult Restore(string fileName)
    {
        lock (_gate)
        {
            string? operationRoot = null;
            bool journalWritten = false;
            try
            {
                RequireOffline();
                if (HasPendingRecovery)
                    return new(false, "An interrupted recovery must be explicitly resolved before selecting another backup.", null);
                string operationId = Guid.NewGuid().ToString("N");
                operationRoot = OperationRoot(operationId);
                Directory.CreateDirectory(operationRoot);
                PreparedBackup prepared = PrepareBackup(fileName, Path.Combine(operationRoot, "staging"));
                _fault?.Invoke("recovery-after-validation");

                List<FileStream> leases = [];
                List<OriginalFile> originals = [];
                try
                {
                    Directory.CreateDirectory(Path.Combine(operationRoot, "original"));
                    foreach (string name in StateFiles)
                    {
                        string source = StatePath(name);
                        if (!File.Exists(source)) continue;
                        var lease = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None);
                        leases.Add(lease);
                        string archived = Path.Combine(operationRoot, "original", name);
                        CopyFlushed(lease, archived);
                        originals.Add(new(name, Hash(archived), new FileInfo(archived).Length));
                    }
                    var journal = new RecoveryJournal(1, operationId, fileName, "prepared",
                        originals.ToArray(), Hash(prepared.DatabasePath));
                    WriteJournal(journal);
                    journalWritten = true;
                    _fault?.Invoke("recovery-after-journal");
                }
                finally
                {
                    foreach (FileStream lease in leases) lease.Dispose();
                }

                RequireOffline();
                Directory.CreateDirectory(Path.Combine(operationRoot, "displaced"));
                foreach (string name in StateFiles.Skip(1))
                {
                    string sidecar = StatePath(name);
                    if (File.Exists(sidecar))
                        File.Move(sidecar, Path.Combine(operationRoot, "displaced", name));
                }
                _fault?.Invoke("recovery-after-sidecars");
                string destination = StatePath(DatabaseName);
                if (File.Exists(destination))
                    File.Replace(prepared.DatabasePath, destination, Path.Combine(operationRoot, "displaced", DatabaseName));
                else
                    File.Move(prepared.DatabasePath, destination);
                _fault?.Invoke("recovery-after-replace");
                RecoveryJournal completed = ReadJournal() with { Phase = "completed" };
                if (!Hash(destination).Equals(completed.StagedSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("Installed database differs from the verified recovery stage.");
                WriteJournal(completed);
                _fault?.Invoke("recovery-after-completed-journal");
                ArchiveJournal(operationRoot, "completed-journal.json");
                return new(true, null, operationRoot);
            }
            catch (Exception exception)
            {
                // Fail closed. Originals + pending journal remain intact; never initialize a possibly mixed state.
                return new(false, exception.Message +
                    (journalWritten ? " Recovery is pending; explicitly roll it back before normal startup." : ""), operationRoot);
            }
        }
    }

    /// <summary>
    /// Explicit restart-time resolution. A completed journal is finalized; an interrupted installation
    /// is rolled back to byte-identical archived originals. It never silently imports a backup.
    /// </summary>
    public StateBackupRecoveryResult RecoverInterruptedRestore()
    {
        lock (_gate)
        {
            string? operationRoot = null;
            try
            {
                RequireOffline();
                if (!HasPendingRecovery) return new(true, null, null);
                RecoveryJournal journal = ReadJournal();
                operationRoot = OperationRoot(journal.OperationId);
                if (journal.Phase == "completed")
                {
                    if (Hash(StatePath(DatabaseName)) != journal.StagedSha256)
                        throw new InvalidDataException("Completed recovery no longer matches its journal; manual inspection is required.");
                    ArchiveJournal(operationRoot, "completed-journal.json");
                    return new(true, null, operationRoot);
                }
                if (journal.Phase is not ("prepared" or "rollback"))
                    throw new InvalidDataException("Unknown recovery journal phase.");

                foreach (OriginalFile original in journal.Originals)
                {
                    string source = Path.Combine(operationRoot, "original", original.Name);
                    LocalDataFileSafety.Normalize(source);
                    if (!File.Exists(source) || new FileInfo(source).Length != original.Length ||
                        Hash(source) != original.Sha256)
                        throw new InvalidDataException("An original recovery archive file is missing or has changed.");
                }

                // Verify no SQLite connections still own any current file before changing the set.
                foreach (string name in StateFiles)
                {
                    string current = StatePath(name);
                    if (File.Exists(current))
                    {
                        using var lease = new FileStream(current, FileMode.Open, FileAccess.Read, FileShare.None);
                    }
                }
                WriteJournal(journal with { Phase = "rollback" });
                string round = Path.Combine(operationRoot, "rollback-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(round);
                foreach (string name in StateFiles)
                {
                    OriginalFile? original = journal.Originals.SingleOrDefault(file => file.Name == name);
                    string destination = StatePath(name);
                    if (original is null)
                    {
                        if (File.Exists(destination)) File.Move(destination, Path.Combine(round, name));
                        continue;
                    }
                    string staged = Path.Combine(round, name + ".restore");
                    using (var input = new FileStream(Path.Combine(operationRoot, "original", name),
                        FileMode.Open, FileAccess.Read, FileShare.Read))
                        CopyFlushed(input, staged);
                    if (File.Exists(destination))
                        File.Replace(staged, destination, Path.Combine(round, name));
                    else File.Move(staged, destination);
                    _fault?.Invoke("recovery-rollback-file");
                }
                foreach (OriginalFile original in journal.Originals)
                    if (Hash(StatePath(original.Name)) != original.Sha256)
                        throw new InvalidDataException("Original state rollback did not verify.");
                ArchiveJournal(operationRoot, "rolled-back-journal.json");
                return new(true, null, operationRoot);
            }
            catch (Exception exception)
            {
                return new(false, exception.Message + " Normal database startup remains blocked.", operationRoot);
            }
        }
    }

    private PreparedBackup PrepareBackup(string fileName, string validationRoot)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) ||
            fileName.Contains('/') || fileName.Contains('\\') ||
            !fileName.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a backup filename from this profile's backup directory.");
        string source = LocalDataFileSafety.Normalize(Path.Combine(_paths.Backups, fileName));
        if (!LocalDataFileSafety.IsWithin(source, _paths.Backups))
            throw new InvalidDataException("Backup is outside this profile.");
        if (File.Exists(source + "-wal") || File.Exists(source + "-shm"))
            throw new InvalidDataException("Backup must be a standalone, closed SQLite snapshot.");

        LocalDataFileSafety.Normalize(Path.Combine(validationRoot, ".validation-path"));
        var stagedPaths = new UserDataPaths(validationRoot);
        stagedPaths.EnsureCreated();
        string stagedDatabase = Path.Combine(stagedPaths.State, DatabaseName);
        int sourceVersion;
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (input.Length is < 100 or > MaximumBackupBytes)
                throw new InvalidDataException("Backup size is outside supported bounds.");
            Span<byte> header = stackalloc byte[100];
            input.ReadExactly(header);
            if (!header[..16].SequenceEqual("SQLite format 3\0"u8) ||
                BinaryPrimitives.ReadInt32BigEndian(header[68..72]) != 0x44555033)
                throw new InvalidDataException("Backup is not a DesktopUp state database.");
            sourceVersion = BinaryPrimitives.ReadInt32BigEndian(header[60..64]);
            using var versionProbe = new SqliteStateStore(stagedPaths);
            if (sourceVersion < 1 || sourceVersion > versionProbe.LatestSchemaVersion)
                throw new InvalidDataException("Backup schema is unsupported or newer than this application.");
            input.Position = 0;
            CopyFlushed(input, stagedDatabase);
        }
        using var store = new SqliteStateStore(stagedPaths);
        StateStoreInitializationResult initialized = store.Initialize();
        if (!initialized.Succeeded)
            throw new InvalidDataException("Backup validation/migration failed: " + initialized.Error);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = stagedDatabase, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read() || reader.GetString(0) != "ok" || reader.Read())
                    throw new InvalidDataException("Backup integrity_check failed.");
            }
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            command.ExecuteNonQuery();
            command.CommandText = "PRAGMA journal_mode=DELETE;";
            if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Recovery stage could not close its WAL.");
        }
        if (File.Exists(stagedDatabase + "-wal") || File.Exists(stagedDatabase + "-shm"))
            throw new InvalidDataException("Recovery stage still has SQLite sidecars.");
        return new(stagedDatabase, sourceVersion, initialized.Head!.Revision);
    }

    private void RequireOffline()
    {
        if (!_canRestoreOffline())
            throw new InvalidOperationException("Recovery requires the primary application lease and no initialized state store.");
        _paths.EnsureCreated();
        LocalDataFileSafety.Normalize(Path.Combine(_paths.State, ".recovery-path"));
    }

    private string StatePath(string name) => LocalDataFileSafety.Normalize(Path.Combine(_paths.State, name));
    private string OperationRoot(string operationId)
    {
        if (operationId.Length != 32 || operationId.Any(character => !char.IsAsciiHexDigit(character)))
            throw new InvalidDataException("Recovery operation identifier is invalid.");
        string root = Path.Combine(_paths.State, "recovery", operationId);
        LocalDataFileSafety.Normalize(Path.Combine(root, ".recovery-path"));
        return root;
    }

    private RecoveryJournal ReadJournal()
    {
        using var file = new FileStream(StatePath(JournalName), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 16 * 1024) throw new InvalidDataException("Recovery journal is oversized.");
        var journal = JsonSerializer.Deserialize<RecoveryJournal>(file)
            ?? throw new InvalidDataException("Recovery journal is empty.");
        if (journal.Version != 1 || journal.Originals is null || journal.Originals.Length > StateFiles.Length ||
            journal.Originals.Select(file => file.Name).Distinct(StringComparer.Ordinal).Count() != journal.Originals.Length ||
            journal.Originals.Any(file => !StateFiles.Contains(file.Name, StringComparer.Ordinal) ||
                file.Length < 0 || file.Sha256.Length != 64) || journal.StagedSha256.Length != 64)
            throw new InvalidDataException("Recovery journal is malformed.");
        _ = OperationRoot(journal.OperationId);
        return journal;
    }

    private void WriteJournal(RecoveryJournal journal)
    {
        string destination = StatePath(JournalName);
        string temporary = Path.Combine(_paths.State, ".recovery-journal-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(file, journal);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void ArchiveJournal(string operationRoot, string name) =>
        File.Move(StatePath(JournalName), Path.Combine(operationRoot, name), overwrite: true);

    private static void CopyFlushed(Stream input, string destination)
    {
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 81920, FileOptions.WriteThrough);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PreparedBackup(string DatabasePath, int SourceSchemaVersion, long Revision);
    private sealed record OriginalFile(string Name, string Sha256, long Length);
    private sealed record RecoveryJournal(int Version, string OperationId, string SourceFileName,
        string Phase, OriginalFile[] Originals, string StagedSha256);
}
