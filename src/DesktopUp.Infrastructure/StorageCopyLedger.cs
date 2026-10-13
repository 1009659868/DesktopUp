using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

internal sealed record StorageCopyEntry(LocalTransferObject Object,LocalTransferFiles.CopyMetadata Metadata);
internal sealed record StorageCopyCollection(Guid Id,string MetadataName,StorageCopyEntry[] Entries);
internal sealed record StorageCopyManifest(int FormatVersion,string ProfileIdentity,Guid OperationId,string Fingerprint,StorageCopyCollection[] Collections);
internal sealed record StorageCreatedObject(string RelativePath,LocalTransferObject Object);
internal sealed record StorageVerifiedCopies(StorageBundleClaim Claim,LocalTransferObject[] Targets);
internal sealed record StorageRelocationResult(bool Committed,bool Cleaned,string[] RetainedPaths,string? Reason);

// Immutable, write-through operational evidence, not part of portable settings. The DB intent
// precedes this directory; the existing external-file-history guard prevents restoring a DB
// backup over file history. A torn/unknown receipt is preserved, never interpreted as permission.
internal sealed class StorageCopyLedger(SqliteStateStore store,StorageRelocationPlan plan):IDisposable
{
    private static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,MaxDepth=32};
    private IDisposable? directoryLease;
    internal string DirectoryPath=>Path.Combine(store.StorageRelocationDirectory,plan.Request.OperationId.ToString("N"));
    internal bool Exists(string name)=>LocalTransferFiles.Exists(Path.Combine(DirectoryPath,name));
    internal void Open(bool create)
    {
        if(store.TransferProfileIdentity!=plan.Reads.ProfileIdentity)throw new IOException("Foreign relocation evidence.");
        if(create)Directory.CreateDirectory(store.StorageRelocationDirectory);
        using var parent=LocalTransferFiles.LockDirectory(store.StorageRelocationDirectory);
        if(create)LocalTransferFiles.CreateOwnedDirectory(DirectoryPath);
        directoryLease=LocalTransferFiles.LockDirectory(DirectoryPath);
    }
    internal void Write<T>(string name,T value)
    {
        if(directoryLease is null)throw new InvalidOperationException("Ledger lease required.");
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(value,Json);if(data.Length>4*1024*1024)throw new IOException("Copy evidence budget exceeded.");
        using var file=new FileStream(Path.Combine(DirectoryPath,name),FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);
        file.Write(data);file.Flush(true);
    }
    internal T Read<T>(string name)
    {
        if(directoryLease is null)throw new InvalidOperationException("Ledger lease required.");
        string path=Path.Combine(DirectoryPath,name);using var pin=new LocalTransferFiles.PinnedObject(path);
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length>4*1024*1024)throw new IOException("Copy evidence budget exceeded.");
        return JsonSerializer.Deserialize<T>(file,Json)??throw new InvalidDataException("Empty copy evidence.");
    }
    internal StorageCopyManifest Manifest()
    {
        var m=Read<StorageCopyManifest>("manifest.json");
        if(m.FormatVersion!=1||m.ProfileIdentity!=plan.Reads.ProfileIdentity||m.OperationId!=plan.Request.OperationId||m.Fingerprint!=plan.Fingerprint||
            m.Collections is null||m.Collections.Any(c=>c is null||c.Entries is null||c.Entries.Any(e=>e is null||e.Object is null||e.Metadata is null))||
            m.Collections.Length!=plan.Request.Inventories.Count||m.Collections.Select(c=>c.Id).Distinct().Count()!=m.Collections.Length)
            throw new InvalidDataException("Copy manifest is not bound to this intent.");
        int count=0;long bytes=0;
        foreach(var collection in m.Collections)
        {
            var expected=plan.Request.Inventories.Single(x=>x.CollectionId==collection.Id);var objects=collection.Entries.Select(x=>x.Object).ToArray();
            if(objects.Length==0||objects[0].RelativePath!=""||!objects[0].Directory||objects[0].Identity!=expected.DirectoryIdentity||
                LocalTransferFiles.TreeHash(objects)!=expected.TreeWitness||objects.LongCount(x=>!x.Directory)!=expected.FileCount||objects.Sum(x=>x.Length)!=expected.ByteCount||
                collection.MetadataName is not (".desktopup-collection-owner.json" or ".desktopup-collection-adoption.json" or ".desktopup-collection-bundle.json")||
                objects.Count(x=>x.RelativePath==collection.MetadataName)!=1||objects.Select(x=>x.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=objects.Length)
                throw new InvalidDataException("Copy inventory changed or is incomplete.");
            foreach(var obj in objects){_ = Source(collection.Id,obj.RelativePath);count++;bytes+=obj.Length;}
        }
        if(count>LocalTransferFiles.MaximumTreeEntries||bytes>LocalTransferFiles.MaximumTreeBytes)throw new IOException("Whole-storage copy budget exceeded.");
        return m;
    }
    internal string Source(Guid collectionId,string relative)=>Under(plan.Catalog.Collections.Single(x=>x.Id==collectionId).StorageLocator!,relative);
    internal string Target(Guid collectionId,string relative)=>Under(plan.Request.Destination.Locator,
        Path.Combine("collection-"+collectionId.ToString("N"),relative));
    internal string Destination(string relative)=>Under(plan.Request.Destination.Locator,relative);
    internal static IEnumerable<StorageCopyEntry> Payload(StorageCopyCollection c)=>c.Entries.Where(x=>x.Object.RelativePath!=""&&x.Object.RelativePath!=c.MetadataName);
    private static string Under(string parent,string relative)
    {
        if(relative!=""&&(Path.IsPathRooted(relative)||relative.Split('\\','/').Any(x=>x is "" or "." or "..")))throw new InvalidDataException("Unsafe copy-relative path.");
        string path=LocalTransferFiles.Normalize(Path.Combine(parent,relative));
        if(path!=parent&&!LocalTransferFiles.Contains(parent,path))throw new InvalidDataException("Copy path escaped its owner.");return path;
    }
    internal StorageCreatedObject[] Created(StorageCopyManifest manifest)
    {
        var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){StorageBundleOwnership.RootMarker};
        foreach(var c in manifest.Collections)
        {
            string child="collection-"+c.Id.ToString("N");allowed.Add(child);allowed.Add(Path.Combine(child,StorageBundleOwnership.ChildMarker));
            foreach(var e in Payload(c))allowed.Add(Path.Combine(child,e.Object.RelativePath));
        }
        var files=Directory.EnumerateFiles(DirectoryPath,"created-*.json").Order(StringComparer.Ordinal).Take(8194).ToArray();
        if(files.Length>8193)throw new IOException("Creation receipt budget exceeded.");
        var result=new List<StorageCreatedObject>();
        for(int i=0;i<files.Length;i++)
        {
            string name=$"created-{i:D5}.json";if(Path.GetFileName(files[i])!=name)throw new InvalidDataException("Creation receipt gap.");
            var entry=Read<StorageCreatedObject>(name);_=Destination(entry.RelativePath);
            if(entry.Object is null||!allowed.Contains(entry.RelativePath)||result.Any(x=>string.Equals(x.RelativePath,entry.RelativePath,StringComparison.OrdinalIgnoreCase))||string.IsNullOrWhiteSpace(entry.Object.Identity))
                throw new InvalidDataException("Unbound creation receipt.");result.Add(entry);
        }
        return result.ToArray();
    }
    public void Dispose()=>directoryLease?.Dispose();
}
