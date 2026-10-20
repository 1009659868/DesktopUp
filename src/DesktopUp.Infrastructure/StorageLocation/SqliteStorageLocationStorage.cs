using DesktopUp.Application;
using System.Text.Json;
namespace DesktopUp.Infrastructure;

// Storage facts and cohesive native ownership checks; application decisions live in StorageLocationUseCase.
public sealed class SqliteStorageLocationStorage(SqliteStateStore store, StorageProfileKind kind,
    string localApplicationData, string? isolatedStorageBase = null, Action<string>? creationCheckpoint = null)
    : IStorageLocationStorage
{
    private SqliteStorageRelocationJournal Journal => new(store);
    public StorageDefaultContext DefaultContext() => new(store.TransferProfileIdentity, kind,
        localApplicationData, isolatedStorageBase, [store.StorageProfileRoot, AppContext.BaseDirectory, Path.GetTempPath()]);
    public StorageLocationState ReadLocation() => Journal.ReadLocation();
    public RegisteredManagedStorage[] Locations()
    {
        var result=new SqliteTransferJournal(store).StorageLocations().ToArray();
        long count=store.ExecuteRead((c,t)=>{using var q=SqliteStorageRelocationJournal.Command(c,t,"SELECT count(*) FROM managed_storage_roots");return Convert.ToInt64(q.ExecuteScalar());});
        if(count!=result.Select(x=>x.Root.Id).Distinct().Count())throw new IOException("旧目录缺少集合归属，不能自动另建位置或迁移。");
        return result;
    }
    public void ValidateAvailable(IEnumerable<RegisteredManagedStorage> locations)
    {
        foreach(var location in locations)
        { using var root=LocalTransferFiles.LockDirectory(location.Root.Locator);using var child=LocalTransferFiles.LockDirectory(location.Collection.StorageLocator!);
          ManagedStorageProvisioner.ValidateRegistration(store,location.Root,location.Collection); }
    }
    public void ValidateOwnership(IEnumerable<RegisteredManagedStorage> locations)
    {
        foreach(var location in locations)ManagedStorageProvisioner.ValidateRegistration(store,location.Root,location.Collection);
    }

    public void Select(Guid rootId, StorageLocationState expected) => Journal.Select(rootId, expected);
    public StorageProvisionPreview PreviewProvision(string destination) => new ManagedStorageProvisioner(store).Preview(destination);
    public StorageProvisionPreview PreviewEmptyProvision(string destination)
    {
        if (Directory.EnumerateFileSystemEntries(destination).Any()) throw new IOException("请选择空的专用文件夹，不接管或覆盖已有内容。");
        return PreviewProvision(destination);
    }
    public RegisteredManagedStorage ConfirmProvision(StorageProvisionPreview preview, string confirmation) =>
        new ManagedStorageProvisioner(store).Confirm(preview, confirmation);
    public IDisposable LockEmptyDestination(string destination)
    {
        var lease = LocalTransferFiles.LockDirectory(destination);
        try
        {
            if (Directory.EnumerateFileSystemEntries(destination).Any()) throw new IOException("目标不再为空，请重新选择；现有文件保留。");
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
    public StorageRelocationPlan PreviewRelocation(string destination) => Journal.Preview(destination);
    public StoragePayloadSummary DescribePayload(StorageRelocationPlan plan)
    {
        long files=0,dirs=0,bytes=0;
        using var lease=Journal.LockOwnedSources(plan);
        foreach(var inventory in plan.Request.Inventories)
        {
            string path=plan.Catalog.Collections.Single(x=>x.Id==inventory.CollectionId).StorageLocator!;
            var markers=SqliteStorageRelocationJournal.OwnerFiles(path).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in LocalTransferFiles.Inspect(path,path).Objects)
            {
                if(entry.RelativePath==""||markers.Contains(entry.RelativePath))continue;
                if(entry.Directory)dirs++;else{files++;bytes+=entry.Length;}
            }
        }
        Journal.RequireCurrentPlan(plan);
        var snapshot=new SqliteStateCommandRepository(store).Read();
        var ids=plan.AffectedPosterIds.ToHashSet();
        return new(files,dirs,bytes,snapshot.Catalog.Posters.Where(x=>ids.Contains(x.Poster.PosterId)).Select(x=>x.Poster.Title).ToArray());
    }
    public IReadOnlyList<StorageRelocationStatus> Relocations() => Journal.Read()
        .Select(row => new StorageRelocationStatus(row.Plan.Request.OperationId, row.Phase, row.Closure)).ToArray();
    private sealed record DefaultReceipt(int Version,string Profile,string Path,string? Identity);
    public void PrepareDefaultDirectory(string root,CancellationToken token)
    {
        using var executor=new SqliteTransferJournal(store).AcquireExecutor();
        if(Locations().Length!=0)throw new IOException("收纳位置刚发生变化，请重试。");
        if(store.ProtectedTransferPath(root,true))throw new IOException("默认位置与受保护目录重叠。");
        string evidence=Path.Combine(store.StorageProvisionDirectory,"default-root");Directory.CreateDirectory(evidence);
        string intentPath=Path.Combine(evidence,"intent.json"),receiptPath=Path.Combine(evidence,"created.json");
        var intent=new DefaultReceipt(1,store.TransferProfileIdentity,root,null);
        if(File.Exists(intentPath))
        {
            if(ReadReceipt(intentPath)!=intent)throw new IOException("默认位置创建记录不属于此配置；保留目录。");
        }
        else { store.PreserveTransferGuard();WriteNew(intentPath,intent); }
        creationCheckpoint?.Invoke("IntentWritten");
        if(File.Exists(receiptPath))
        {
            var saved=ReadReceipt(receiptPath);
            if(saved with {Identity=null}!=intent || saved.Identity!=LocalTransferFiles.Identity(root))
                throw new IOException("默认收纳目录被替换，原内容保持，不自动接管。");
            return;
        }
        if(Directory.Exists(root)||File.Exists(root))throw new IOException("默认路径已存在但无法证明归属；请更改位置，不接管现有文件。");
        // Only create absent ancestors one at a time, holding the native parent chain per creation.
        var missing=new Stack<string>();string? parent=Path.GetDirectoryName(root);
        while(parent is {Length:>3}&&!Directory.Exists(parent)){missing.Push(parent);parent=Path.GetDirectoryName(parent);}
        while(missing.TryPop(out var path)){token.ThrowIfCancellationRequested();LocalTransferFiles.CreateOwnedDirectory(path);}
        token.ThrowIfCancellationRequested();var created=LocalTransferFiles.CreateOwnedDirectory(root);
        creationCheckpoint?.Invoke("DirectoryCreated");
        WriteNew(receiptPath,intent with {Identity=created.Identity});
        creationCheckpoint?.Invoke("ReceiptWritten");
    }
    private static DefaultReceipt ReadReceipt(string path)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length>32768)throw new IOException("默认位置凭证超限。");
        return JsonSerializer.Deserialize<DefaultReceipt>(input)??throw new IOException("默认位置凭证损坏。");
    }
    private static void WriteNew<T>(string path,T value)
    { using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);output.Write(JsonSerializer.SerializeToUtf8Bytes(value));output.Flush(true); }

}
