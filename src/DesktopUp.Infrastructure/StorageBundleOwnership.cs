using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

internal sealed record StorageBundleCollection(Guid CollectionId,string Locator,string ObjectIdentity);
internal sealed record StorageBundleClaim(int FormatVersion,string ProfileIdentity,Guid OperationId,string PlanFingerprint,
    ManagedStorageRoot Root,IReadOnlyList<StorageBundleCollection> Collections);
internal sealed record StorageBundleChild(int FormatVersion,string ProfileIdentity,Guid OperationId,string PlanFingerprint,
    Guid RootId,StorageBundleCollection Collection);

// A separate v2 declaration, never a rewrite of v1 or adoption markers. It becomes active
// only with a matching durable commit in the owning physical profile. Contains no file executor.
internal static class StorageBundleOwnership
{
    internal const string RootMarker=".desktopup-storage-bundle.json",ChildMarker=".desktopup-collection-bundle.json";
    private static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,MaxDepth=16};

    internal static StorageBundleClaim CreatePrepared(SqliteStateStore store,StorageRelocationPlan plan,
        Action<string,LocalTransferObject>? created=null)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        var row=new SqliteStorageRelocationJournal(store).Get(plan.Request.OperationId);
        if(row.Plan.Fingerprint!=plan.Fingerprint||row.Commit is not null||row.Closure is not null||plan.Reads.RuntimeGeneration!=store.StorageRuntimeGeneration)
            throw new IOException("Current durable migration intent required before creating declarations.");
        var root=plan.Request.Destination;
        if(store.ProtectedTransferPath(root.Locator,true)||store.TransferProfileIdentity!=plan.Reads.ProfileIdentity)throw new IOException("Foreign/protected storage.");
        using var rootLease=LocalTransferFiles.LockDirectory(root.Locator);
        if(LocalTransferFiles.Identity(root.Locator)!=root.ObjectIdentity||Directory.EnumerateFileSystemEntries(root.Locator).Any())
            throw new IOException("Only the original empty target may be prepared; partial/foreign directories are preserved.");
        var bindings=new List<StorageBundleCollection>();
        foreach(var inventory in plan.Request.Inventories.OrderBy(x=>x.CollectionId))
        {
            string child=Path.Combine(root.Locator,"collection-"+inventory.CollectionId.ToString("N"));
            var createdChild=LocalTransferFiles.CreateOwnedDirectory(child);created?.Invoke(child,createdChild);
            using var childLease=LocalTransferFiles.LockDirectory(child);
            var binding=new StorageBundleCollection(inventory.CollectionId,child,LocalTransferFiles.Identity(child));bindings.Add(binding);
            WriteNew(Path.Combine(child,ChildMarker),new StorageBundleChild(2,plan.Reads.ProfileIdentity,plan.Request.OperationId,plan.Fingerprint,root.Id,binding));
            if(created is not null){using var marker=new LocalTransferFiles.PinnedObject(Path.Combine(child,ChildMarker));created(Path.Combine(child,ChildMarker),marker.Snapshot());}
        }
        var claim=new StorageBundleClaim(2,plan.Reads.ProfileIdentity,plan.Request.OperationId,plan.Fingerprint,root,Array.AsReadOnly(bindings.ToArray()));
        WriteNew(Path.Combine(root.Locator,RootMarker),claim);
        if(created is not null){using var marker=new LocalTransferFiles.PinnedObject(Path.Combine(root.Locator,RootMarker));created(Path.Combine(root.Locator,RootMarker),marker.Snapshot());}
        using var check=LockPrepared(store,plan,claim);return claim;
    }
    internal static void ValidateShape(StorageRelocationPlan plan,StorageBundleClaim claim)
    {
        if(claim.FormatVersion!=2||claim.ProfileIdentity!=plan.Reads.ProfileIdentity||claim.OperationId!=plan.Request.OperationId||
            claim.PlanFingerprint!=plan.Fingerprint||claim.Root!=plan.Request.Destination||claim.Collections.Count!=plan.Request.Inventories.Count||
            !claim.Collections.Select(x=>x.CollectionId).ToHashSet().SetEquals(plan.Request.Inventories.Select(x=>x.CollectionId))||
            claim.Collections.Select(x=>x.ObjectIdentity).Distinct().Count()!=claim.Collections.Count||
            claim.Collections.Any(x=>string.IsNullOrWhiteSpace(x.ObjectIdentity)||x.Locator!=Path.Combine(claim.Root.Locator,"collection-"+x.CollectionId.ToString("N"))))
            throw new IOException("Invalid multi-collection declaration.");
    }
    internal static SqliteStorageRelocationJournal.Leases LockPrepared(SqliteStateStore store,StorageRelocationPlan plan,StorageBundleClaim expected)
    {
        ValidateShape(plan,expected);
        if(expected.ProfileIdentity!=store.TransferProfileIdentity||store.ProtectedTransferPath(expected.Root.Locator,true))throw new IOException("Foreign/protected declaration.");
        var leases=new SqliteStorageRelocationJournal.Leases();
        try
        {
            leases.Add(LocalTransferFiles.LockDirectory(expected.Root.Locator));
            if(LocalTransferFiles.Identity(expected.Root.Locator)!=expected.Root.ObjectIdentity)throw new IOException("Storage root was replaced.");
            RejectLegacy(expected.Root.Locator,true);
            var root=Read<StorageBundleClaim>(Path.Combine(expected.Root.Locator,RootMarker),leases);
            if(SqliteStorageRelocationJournal.Encode(root)!=SqliteStorageRelocationJournal.Encode(expected))throw new IOException("Storage owner declaration changed.");
            foreach(var collection in expected.Collections)
            {
                leases.Add(LocalTransferFiles.LockDirectory(collection.Locator));RejectLegacy(collection.Locator,false);
                if(LocalTransferFiles.Identity(collection.Locator)!=collection.ObjectIdentity)throw new IOException("Collection was replaced.");
                var child=Read<StorageBundleChild>(Path.Combine(collection.Locator,ChildMarker),leases);
                if(child!=new StorageBundleChild(2,expected.ProfileIdentity,expected.OperationId,expected.PlanFingerprint,expected.Root.Id,collection))
                    throw new IOException("Child declaration does not match root/operation.");
            }
            return leases;
        }
        catch {leases.Dispose();throw;}
    }
    internal static void ValidateActive(SqliteStateStore store,ManagedStorageRoot root,Collection collection)
    {
        using var lease=new SqliteStorageRelocationJournal.Leases();
        var declaration=Read<StorageBundleClaim>(Path.Combine(root.Locator,RootMarker),lease);
        var row=new SqliteStorageRelocationJournal(store).Get(declaration.OperationId);
        if(row.Commit is null||row.Commit.ActiveRootId!=root.Id||declaration.Root!=root||
            row.Targets is null||SqliteStorageRelocationJournal.Encode(row.Targets.Claim)!=SqliteStorageRelocationJournal.Encode(declaration)||
            declaration.Collections.All(x=>x.CollectionId!=collection.Id||x.Locator!=collection.StorageLocator))
            throw new IOException("Prepared/foreign declarations cannot activate storage.");
        using var checkedOwners=LockPrepared(store,row.Plan,declaration);
        var registered=new SqliteTransferJournal(store).StorageLocations();
        if(!registered.Any(x=>x.Root==root&&x.Collection==collection))throw new IOException("Storage is not currently registered.");
    }
    private static T Read<T>(string path,SqliteStorageRelocationJournal.Leases leases)
    {
        _=LocalTransferFiles.Identity(path);var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);leases.Add(file);
        if(file.Length>1024*1024)throw new IOException("Storage declaration budget exceeded.");
        return JsonSerializer.Deserialize<T>(file,Json)??throw new IOException("Empty declaration.");
    }
    private static void WriteNew<T>(string path,T value)
    {using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);file.Write(JsonSerializer.SerializeToUtf8Bytes(value,Json));file.Flush(true);}
    private static void RejectLegacy(string path,bool root)
    {
        if(File.Exists(Path.Combine(path,root?".desktopup-storage-owner.json":".desktopup-collection-owner.json"))||
            File.Exists(Path.Combine(path,root?ManagedStorageAdoption.RootMarker:ManagedStorageAdoption.ChildMarker)))throw new IOException("Conflicting legacy ownership declaration.");
    }
}
