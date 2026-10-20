namespace DesktopUp.Application;

/// <summary>Bounded data-only profile IO; adapters enforce local paths and atomic, consented replacement.</summary>
public interface IPortableProfileFiles
{
    byte[] Read(string path);
    void WriteAtomic(string path, ReadOnlyMemory<byte> bytes, bool overwriteConfirmed);
}

public sealed record StateBackupCandidate(
    string FileName, DateTime LastWriteUtc, bool IsValid, int SchemaVersion, long? Revision, string? Error);
public sealed record StateBackupRecoveryResult(bool Succeeded, string? Error, string? ArchivePath);

/// <summary>Offline atomic recovery. The host must hold the single-instance lease and keep the live store closed.
/// Implementations own validation, exclusive access, durable replacement and interrupted rollback as one operation.</summary>
public interface IStateBackupRecovery
{
    bool HasPendingRecovery { get; }
    IReadOnlyList<StateBackupCandidate> ListCandidates();
    StateBackupRecoveryResult Restore(string fileName);
    StateBackupRecoveryResult RecoverInterruptedRestore();
}
