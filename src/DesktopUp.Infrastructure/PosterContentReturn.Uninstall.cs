using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed record UninstallContentInventory(long Revision, IReadOnlyList<ShellExitItem> Files, IReadOnlyList<string> Locations);
public sealed record UninstallContentResult(bool Completed, IReadOnlyList<string> Returned, IReadOnlyList<string> Retained, IReadOnlyList<string> Errors);

public sealed partial class PosterContentReturn
{
    private const string UninstallScope = "@uninstall";

    // Explicit preservation never needs the content disk online and never settles/replays
    // a pending move. The database and all recorded directories are retained for reinstall.
    public IReadOnlyList<string> RetainedLocations() => new SqliteTransferJournal(store).StorageLocations()
        .SelectMany(r=>new[]{r.Root.Locator,r.Collection.StorageLocator!}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public UninstallContentInventory InspectUninstall()
    {
        using var lease = new SqliteTransferJournal(store).AcquireExecutor();
        return Inventory();
    }

    private UninstallContentInventory Inventory()
    {
        var state = State; var journal = new SqliteTransferJournal(store);
        store.ExecuteRead((c,t) => { SqliteStorageRelocationJournal.RequireNoPending(c,t); return 0; });
        if (journal.Read().Any(x => !x.IsFinished) || MappedDirectoryIngress.Pending(store) || ContentReturnStore.Pending(store))
            throw new IOException("存在尚未核清的文件操作；请先正常启动检查。文件未移动，卸载未执行。");
        var registrations = journal.StorageLocations();
        long roots = store.ExecuteRead((c,t) => { using var q=c.CreateCommand(); q.Transaction=t; q.CommandText="SELECT count(*) FROM managed_storage_roots"; return Convert.ToInt64(q.ExecuteScalar()); });
        if (roots != registrations.Select(r=>r.Root.Id).Distinct().Count()) throw new IOException("收纳目录归属不完整，已停止归还与卸载。");
        var files = new List<ShellExitItem>(); var locations = new List<string>();
        foreach (var registration in registrations)
        {
            ManagedStorageProvisioner.ValidateRegistration(store, registration.Root, registration.Collection);
            string directory = registration.Collection.StorageLocator!;
            using var parent = LocalTransferFiles.LockDirectory(directory);
            locations.Add(directory);
            var metadata=SqliteStorageRelocationJournal.OwnerFiles(directory).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
            {
                if(metadata.Contains(path))continue; // Validated ownership belongs to the directory, never a user return item.
                if (store.ProtectedTransferPath(path)) throw new IOException("收纳来源受保护："+path);
                MappedDirectoryIngress.ValidateTree(store,path,CancellationToken.None);
                var matches = state.ContentCatalog.Items.Where(i => string.Equals(i.Target.Locator,path,StringComparison.OrdinalIgnoreCase) &&
                    state.ContentCatalog.Memberships.Any(m=>m.ContentItemId==i.Id && m.CollectionId==registration.Collection.Id)).ToArray();
                if (matches.Length>1) throw new IOException("同一路径的内容身份有歧义，未归还："+path);
                var item=matches.SingleOrDefault();
                files.Add(new(Guid.Empty,item?.Id??Guid.Empty,item?.Version??0,path,LocalTransferFiles.Identity(directory),LocalTransferFiles.Identity(path),false));
            }
        }
        return new(state.Revision,files,locations);
    }

    // Invoked only by an explicit uninstall choice under the application's primary-instance
    // lease. Keeps poster definitions and mapped/external content for a possible reinstall.
    public async Task<UninstallContentResult> ReturnForUninstall(UninstallContentInventory preview, nint owner,
        CancellationToken token = default, Action<string>? progress = null)
    {
        using var lease = new SqliteTransferJournal(store).AcquireExecutor();
        ContentReturnStore.Backfill(store);
        var initial=Inventory();
        if (StateRequestFingerprint.Compute(initial)!=StateRequestFingerprint.Compute(preview))
            return new(false,[],initial.Locations,["内容已变化，请重新卸载；文件未移动。"]);
        var returned=new List<string>(); var errors=new List<string>(); var expected=State;
        bool Current()=>!token.IsCancellationRequested && State.Revision==expected.Revision;
        foreach (var source in initial.Files)
        {
            try
            {
                if(!Current())throw new IOException("已停止归还或配置变化；程序与剩余文件保留。");
                progress?.Invoke(source.Path);
                var result=await ReturnOne(UninstallScope,source,expected.ContentCatalog,Current,owner,token).ConfigureAwait(false);
                expected=result.State; returned.Add(result.Path);
                if(result.Stopped || token.IsCancellationRequested)throw new IOException("已取消；已归还文件保持原位，剩余文件保留。");
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or OperationCanceledException)
            {errors.Add(e.Message);break;}
        }
        try
        {
            var remaining=Inventory();
            if(remaining.Files.Count>0 && errors.Count==0)errors.Add("收纳目录仍有内容；停止卸载，未删除任何剩余文件。");
            if(!Current() && errors.Count==0)errors.Add("配置变化或操作已取消，停止卸载。");
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){errors.Add(e.Message);}
        return new(errors.Count==0,returned,initial.Locations,errors);
    }

    private bool UninstallSourceCurrent(ShellExitItem source, DesktopStateSnapshot state)
    {
        var owned=new SqliteTransferJournal(store).StorageLocations().SingleOrDefault(r=>
            string.Equals(r.Collection.StorageLocator,Path.GetDirectoryName(source.Path),StringComparison.OrdinalIgnoreCase));
        if(owned is null)return false;
        ManagedStorageProvisioner.ValidateRegistration(store,owned.Root,owned.Collection);
        MappedDirectoryIngress.ValidateTree(store,source.Path,CancellationToken.None);
        return source.ContentId==Guid.Empty || state.ContentCatalog.Items.Any(i=>i.Id==source.ContentId && i.Version==source.Version && i.Target.Locator==source.Path &&
            state.ContentCatalog.Memberships.Any(m=>m.ContentItemId==i.Id && m.CollectionId==owned.Collection.Id));
    }

    private DesktopStateSnapshot CommitUninstall(SqliteConnection c, SqliteTransaction t, DesktopStateSnapshot before,
        ContentReturnIntent intent, ContentReturnReceipt receipt)
    {
        var source=intent.Source;
        if(source.ContentId!=Guid.Empty && !before.ContentCatalog.Items.Any(i=>i.Id==source.ContentId && i.Version==source.Version && i.Target.Locator==source.Path))
            throw new IOException("来源记录已变化；文件已保留在："+receipt.Path);
        var owned=before.ContentCatalog.Collections.Where(x=>x.Kind==ContentCollectionKind.Managed).Select(x=>x.Id).ToHashSet();
        var ids=before.ContentCatalog.Items.Where(i=>(string.Equals(i.Target.Locator,source.Path,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(source.Path,i.Target.Locator)) &&
            before.ContentCatalog.Memberships.Any(m=>m.ContentItemId==i.Id && owned.Contains(m.CollectionId))).Select(i=>i.Id).ToHashSet();
        var cards=before.ContentCatalog.PosterItems.Where(p=>before.ContentCatalog.Memberships.Any(m=>m.Id==p.MembershipId && ids.Contains(m.ContentItemId))).ToArray();
        var removed=before.ContentCatalog.Memberships.Where(m=>ids.Contains(m.ContentItemId) && owned.Contains(m.CollectionId)).Select(m=>m.Id).ToArray();
        if(cards.Length==0 && removed.Length==0)return before;
        var removal=ContentRelationRemoval.Prepare(before,cards.Select(p=>p.Id).ToArray());
        if(removal.Rejection is not null)throw new IOException(removal.Error);
        var plan=removal with {ExternalSyncReceiptId=intent.Id,RemovedStorageMemberships=removed,
            ContentCatalog=removal.ContentCatalog! with{Memberships=removal.ContentCatalog!.Memberships.Where(m=>!removed.Contains(m.Id)).ToArray()}};
        var reads=new StateReadSet(before.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),CatalogVersion:before.Catalog.CatalogVersion,ContentVersion:before.ContentVersion);
        var result=SqliteStateCommandRepository.ExecuteInTransaction(c,t,new("content-return-"+intent.Id,StateRequestFingerprint.Compute(receipt),before.Revision,reads),_=>plan,authorizedExternalSyncId:intent.Id);
        if(result.Status is not(StateCommandStatus.Committed or StateCommandStatus.Replayed))throw new IOException(result.Error);
        return result.Snapshot;
    }
}
