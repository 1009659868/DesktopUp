namespace DesktopUp.Application;

public enum StateStoreInitializationStatus
{
    Created,
    Ready,
    Migrated,
    Failed
}

public sealed record StateStoreHead(
    int SchemaVersion,
    long Revision,
    DateTimeOffset UpdatedAtUtc);

public sealed record StateStoreInitializationResult(
    StateStoreInitializationStatus Status,
    int PreviousSchemaVersion,
    StateStoreHead? Head,
    string? PreMigrationBackupFileName,
    string? Error)
{
    public bool Succeeded => Status is not StateStoreInitializationStatus.Failed;
}

public enum StateCompareAndSwapStatus
{
    Committed,
    Conflict,
    Replayed
}

public sealed record StateCompareAndSwapResult(
    StateCompareAndSwapStatus Status,
    string OperationId,
    long ExpectedRevision,
    long CurrentRevision,
    long? CommittedRevision)
{
    public bool Succeeded => Status is
        StateCompareAndSwapStatus.Committed or
        StateCompareAndSwapStatus.Replayed;
}

public sealed record StateBackupResult(
    bool Succeeded,
    string? FileName,
    long? Revision,
    string? Error);

public sealed record StateBackupValidationResult(
    bool IsValid,
    int SchemaVersion,
    long? Revision,
    string? Error);

public interface IStateStore : IDisposable
{
    int LatestSchemaVersion { get; }
    StateStoreInitializationResult Initialize();
    StateStoreHead ReadHead();
    StateBackupResult CreateBackup(string reason = "manual");
    StateBackupValidationResult ValidateBackup(string backupPath);
}
