using DesktopUp.Domain;

namespace DesktopUp.Application;

// A preview describes intent; only the infrastructure adapter can produce native copy proofs.
public sealed record StorageProvisionIntent(int FormatVersion,string ProfileIdentity,ManagedStorageRoot Root,Guid CollectionId);
public sealed class StorageProvisionPreview
{
    public StorageProvisionPreview(StorageProvisionIntent intent,long contentVersion)
    { Intent=intent;ContentVersion=contentVersion;Fingerprint=StateRequestFingerprint.Compute(new {intent,contentVersion}); }
    public StorageProvisionIntent Intent { get; }
    public long ContentVersion { get; }
    public string Fingerprint { get; }
    public string CollectionPath => Path.Combine(Intent.Root.Locator,"collection-"+Intent.CollectionId.ToString("N"));
}
public sealed record StorageLocationView(string Path, bool Exists, bool NeedsSelection,
    IReadOnlyList<RegisteredManagedStorage> Choices, string? Notice);
public sealed record StorageChangeResult(bool Committed, bool Cleaned, IReadOnlyList<string> RetainedPaths, string Message);
public sealed class StorageChangePreview
{
    internal StorageChangePreview(StorageRelocationPlan? plan, StorageProvisionPreview? provision, StorageLocationState location,
        long files, long directories, long bytes, string[] posters)
    { Plan=plan;Provision=provision;Location=location;Files=files;Directories=directories;Bytes=bytes;Posters=posters; }
    internal StorageRelocationPlan? Plan { get; }
    internal StorageProvisionPreview? Provision { get; }
    internal StorageLocationState Location { get; }
    public string Destination => Plan?.Request.Destination.Locator ?? Provision!.Intent.Root.Locator;
    public string Fingerprint => Plan?.Fingerprint ?? Provision!.Fingerprint;
    public long Files { get; }
    public long Directories { get; }
    public long Bytes { get; }
    public IReadOnlyList<string> Posters { get; }
}

public sealed record StorageLocationState(ManagedStorageSelection Selection, long RootSetVersion);
public sealed record StorageDefaultContext(string ProfileIdentity, StorageProfileKind Kind,
    string LocalApplicationData, string? IsolatedStorageBase, IReadOnlyList<string> ProtectedDirectories);
public sealed record StoragePayloadSummary(long Files, long Directories, long Bytes, string[] Posters);
public sealed record StorageRelocationStatus(Guid OperationId, StorageRelocationPhase Phase, string? Closure);
public sealed record StorageRelocationResult(bool Committed, bool Cleaned, string[] RetainedPaths, string? Reason);

public interface IStorageLocationStorage
{
    StorageDefaultContext DefaultContext();
    StorageLocationState ReadLocation();
    RegisteredManagedStorage[] Locations();
    void ValidateAvailable(IEnumerable<RegisteredManagedStorage> locations);
    void ValidateOwnership(IEnumerable<RegisteredManagedStorage> locations);
    void Select(Guid rootId, StorageLocationState expected);
    void PrepareDefaultDirectory(string root, CancellationToken token);
    StorageProvisionPreview PreviewProvision(string destination);
    StorageProvisionPreview PreviewEmptyProvision(string destination);
    RegisteredManagedStorage ConfirmProvision(StorageProvisionPreview preview, string confirmation);
    IDisposable LockEmptyDestination(string destination);
    StorageRelocationPlan PreviewRelocation(string destination);
    StoragePayloadSummary DescribePayload(StorageRelocationPlan plan);
    IReadOnlyList<StorageRelocationStatus> Relocations();
}

// Each stage owns the original leases and real verifier; callers cannot fabricate a successful proof.
public interface IStorageRelocationExecution
{
    void Prepare(StorageRelocationPlan plan, string confirmation, CancellationToken token);
    void RecordVerifiedTargets(Guid operation);
    void CommitVerifiedTargets(Guid operation);
    bool HasIntent(Guid operation);
    StorageRelocationResult FinishObserved(Guid operation, Func<bool>? stopCleanup = null);
}

public sealed record TransferLocationNotice(string Source, string Destination);
public interface ITransferLocationNotices
{
    IReadOnlyList<TransferLocationNotice> Read();
}
