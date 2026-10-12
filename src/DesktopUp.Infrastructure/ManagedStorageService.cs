using System.Text.Json;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

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

// The product facade owns the actual verifier. UI never supplies file proofs or commit receipts.
public sealed class ManagedStorageService(SqliteStateStore store, StorageProfileKind kind,
    string localApplicationData, string? isolatedStorageBase = null)
{
    public string? StartupNotice { get; private set; }
    private SqliteStorageRelocationJournal Journal => new(store);
    private RegisteredManagedStorage[] Locations()
    {
        var result=new SqliteTransferJournal(store).StorageLocations().ToArray();
        long count=store.ExecuteRead((c,t)=>{using var q=SqliteStorageRelocationJournal.Command(c,t,"SELECT count(*) FROM managed_storage_roots");return Convert.ToInt64(q.ExecuteScalar());});
        if(count!=result.Select(x=>x.Root.Id).Distinct().Count())throw new IOException("旧目录缺少集合归属，不能自动另建位置或迁移。");
        return result;
    }
    internal Action<string>? CreationCheckpoint { get; set; }
    public string ProposedDefaultPath => ManagedStorageLocationPolicy.ProposeDefaultPath(store.TransferProfileIdentity,kind,
        localApplicationData,isolatedStorageBase,[store.StorageProfileRoot,AppContext.BaseDirectory,Path.GetTempPath()]);

    public StorageLocationView Read()
    {
        var selection=Journal.ReadLocation();var locations=Locations();
        if(selection.Selection.ActiveRootId is {} id)
        {
            var owned=locations.Where(x=>x.Root.Id==id).ToArray();
            if(owned.Length==0)throw new IOException("原收纳位置未登记，未切换到其他目录。");
            Validate(owned);return new(owned[0].Root.Locator,true,false,locations,StartupNotice);
        }
        var roots=locations.Select(x=>x.Root.Id).Distinct().ToArray();
        if(roots.Length==0)return new(ProposedDefaultPath,false,false,locations,StartupNotice);
        if(roots.Length==1){Validate(locations);return new(locations[0].Root.Locator,true,false,locations,StartupNotice);}
        return new("存在多个旧位置，请明确选择用于新增收纳的位置",false,true,locations,StartupNotice);
    }
    private void Validate(IEnumerable<RegisteredManagedStorage> locations)
    {
        foreach(var location in locations)
        { using var root=LocalTransferFiles.LockDirectory(location.Root.Locator);using var child=LocalTransferFiles.LockDirectory(location.Collection.StorageLocator!);
          ManagedStorageProvisioner.ValidateRegistration(store,location.Root,location.Collection); }
    }
    private void ValidateOwned(IEnumerable<RegisteredManagedStorage> locations)
    {
        foreach(var location in locations)ManagedStorageProvisioner.ValidateRegistration(store,location.Root,location.Collection);
    }
    public void SelectExisting(Guid rootId)
    { Journal.Select(rootId,Journal.ReadLocation()); }

    // Called only on an explicit first use. Merely inspecting settings does not create storage.
    public RegisteredManagedStorage EnsureReady(CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();var view=Read();
        if(view.NeedsSelection)throw new IOException("请先在全局收纳位置中选择已有目录；不会自动合并旧位置。");
        var journal=Journal;
        if(view.Choices.Count==0)
        {
            string root=ProposedDefaultPath;
            PrepareDefaultDirectory(root,cancellation);
            var provisioner=new ManagedStorageProvisioner(store);var preview=provisioner.Preview(root);
            cancellation.ThrowIfCancellationRequested();provisioner.Confirm(preview,preview.Fingerprint);
            CreationCheckpoint?.Invoke("Registered");
        }
        var state=journal.ReadLocation();var all=Locations();
        var chosen=state.Selection.ActiveRootId ?? all.Select(x=>x.Root.Id).Distinct().Single();
        journal.Select(chosen,state);var active=all.Where(x=>x.Root.Id==chosen).OrderBy(x=>x.Collection.Id).ToArray();
        ValidateOwned(active);return active[0];
    }

    private sealed record DefaultReceipt(int Version,string Profile,string Path,string? Identity);
    private void PrepareDefaultDirectory(string root,CancellationToken token)
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
        CreationCheckpoint?.Invoke("IntentWritten");
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
        CreationCheckpoint?.Invoke("DirectoryCreated");
        WriteNew(receiptPath,intent with {Identity=created.Identity});
        CreationCheckpoint?.Invoke("ReceiptWritten");
    }
    private static DefaultReceipt ReadReceipt(string path)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length>32768)throw new IOException("默认位置凭证超限。");
        return JsonSerializer.Deserialize<DefaultReceipt>(input)??throw new IOException("默认位置凭证损坏。");
    }
    private static void WriteNew<T>(string path,T value)
    { using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);output.Write(JsonSerializer.SerializeToUtf8Bytes(value));output.Flush(true); }

    public StorageChangePreview PreviewChange(string destination)
    {
        var location=Journal.ReadLocation();
        if(Locations().Length==0)
        {
            if(Directory.EnumerateFileSystemEntries(destination).Any())throw new IOException("请选择空的专用文件夹，不接管或覆盖已有内容。");
            return new(null,new ManagedStorageProvisioner(store).Preview(destination),location,0,0,0,[]);
        }
        var plan=Journal.Preview(destination);long files=0,dirs=0,bytes=0;
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
        return new(plan,null,location,files,dirs,bytes,snapshot.Catalog.Posters.Where(x=>ids.Contains(x.Poster.PosterId)).Select(x=>x.Poster.Title).ToArray());
    }
    public StorageChangeResult Change(StorageChangePreview preview,string confirmation,CancellationToken cancellation=default)
    {
        if(confirmation!=preview.Fingerprint)throw new IOException("请确认本次位置预览。");
        cancellation.ThrowIfCancellationRequested();
        if(preview.Provision is {} provision)
        {
            if(Journal.ReadLocation()!=preview.Location||Locations().Length!=0)throw new IOException("收纳目录已经变化，请重新预览。");
            using var target=LocalTransferFiles.LockDirectory(preview.Destination);
            if(Directory.EnumerateFileSystemEntries(preview.Destination).Any())throw new IOException("目标不再为空，请重新选择；现有文件保留。");
            var result=new ManagedStorageProvisioner(store).Confirm(provision,confirmation);
            // Registration is durable even if selection is interrupted; a sole old root is selected on next use.
            Journal.Select(result.Root.Id,Journal.ReadLocation());
            return new(true,true,[],"收纳位置已设置；没有文件需要迁移。");
        }
        var copied=new StorageRelocationFiles(store).Execute(preview.Plan!,confirmation,cancellation);
        return new(copied.Committed,copied.Cleaned,copied.RetainedPaths,
            copied.Committed?(copied.Cleaned?"收纳位置已更改，内容已迁移。":"新位置已生效；部分旧副本保留在下列位置。"):
            (copied.Cleaned?"未更改位置；原内容保留，可直接重试。":"未更改位置；无法核实的副本已保留，请查看下列位置。"));
    }
    public IReadOnlyList<StorageChangeResult> Reconcile()
    {
        var results=new List<StorageChangeResult>();
        foreach(var row in Journal.Read().Where(x=>x.Closure is null&&x.Phase!=StorageRelocationPhase.Completed))
        {
            var done=new StorageRelocationFiles(store).Recover(row.Plan.Request.OperationId);
            results.Add(new(done.Committed,done.Cleaned,done.RetainedPaths,done.Committed?"上次更改已生效。":"上次更改未提交，原位置保持。"));
        }
        StartupNotice=results.Count==0?null:string.Join("\n",results.Select(x=>x.Message+(x.Cleaned?"":" 保留的文件位置：\n"+string.Join("\n",x.RetainedPaths))));
        return results;
    }
}
