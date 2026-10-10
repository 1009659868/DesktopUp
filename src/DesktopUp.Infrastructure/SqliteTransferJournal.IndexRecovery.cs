using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// This ticket authorizes only an index repair. It cannot be passed to the move executor.
public sealed class TransferIndexRecoveryTicket
{
    internal TransferIndexRecoveryTicket(DurableTransferItem original,DurableTransferBatch batch,string fingerprint)
    { Original=original;Batch=batch;Fingerprint=fingerprint; }
    internal DurableTransferItem Original { get; }
    internal DurableTransferBatch Batch { get; }
    public string Fingerprint { get; }
    public string Destination => Original.File.Destination;
    public bool IsIncoming => Batch.Request.Route==ContentDropRoute.IntoManagedStorage;
    public string PosterId => Batch.Request.PosterId;
    public IReadOnlyList<string> AffectedPosterIds => Batch.Reads.Posters.Keys.ToArray();
    internal string RequestId => $"transfer-recovery-{Original.OperationId:N}-{Original.Index}-{Fingerprint}";
}

public sealed partial class SqliteTransferJournal
{
    public TransferIndexRecoveryTicket PreviewIndexRecovery(Guid operationId,int index,string? selectedPosterId=null,bool automatic=false)
    {
        using var executor=AcquireExecutor();
        var original=Read().Single(x=>x.OperationId==operationId&&x.Index==index);
        using var files=LockMovedResult(original);
        return store.ExecuteRead((c,t)=>
        {
            var batch=ReadBatch(c,t,operationId);
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            if(automatic && batch.Request.Route==ContentDropRoute.IntoManagedStorage &&
                (!batch.Reads.Posters.TryGetValue(batch.Request.PosterId,out long version) ||
                 state.Catalog.Posters.SingleOrDefault(x=>x.Poster.PosterId==batch.Request.PosterId)?.EntityVersion!=version))
                throw new IOException("原目标已被编辑，不自动重新加入已删除卡片。文件保持原位。");
            return BuildIndexRecovery(original,batch,state,selectedPosterId);
        });
    }

    public StateTransactionResult ConfirmIndexRecovery(TransferIndexRecoveryTicket ticket,string confirmation)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if(ticket.Fingerprint!=confirmation)throw new IOException("恢复预览尚未确认，请重新查看影响范围。");
        using var executor=AcquireExecutor();
        var original=Read().Single(x=>x.OperationId==ticket.Original.OperationId&&x.Index==ticket.Original.Index);
        if(Terminal(original))
            return store.ExecuteRead((c,t)=>
            {
                using var q=Command(c,t,"SELECT request_sha256,committed_revision FROM state_command_receipts WHERE request_id=$id",("$id",ticket.RequestId));
                using var r=q.ExecuteReader();
                if(!r.Read()||r.GetString(0)!=confirmation)throw new IOException("该记录已由其他操作处理，请刷新。");
                return new StateTransactionResult(StateCommandStatus.Replayed,SqliteStateCommandRepository.ReadSnapshot(c,t),
                    r.IsDBNull(1)?null:r.GetInt64(1),ticket.PosterId,[],null);
            });
        using var files=LockMovedResult(original);
        return store.ExecuteWrite((c,t)=>
        {
            var current=ReadItems(c,t).Single(x=>x.OperationId==original.OperationId&&x.Index==original.Index);
            if(StateRequestFingerprint.Compute(current)!=StateRequestFingerprint.Compute(ticket.Original))
                throw new IOException("恢复记录已变化，请重新确认。");
            var state=SqliteStateCommandRepository.ReadSnapshot(c,t);
            var fresh=BuildIndexRecovery(current,ReadBatch(c,t,current.OperationId),state,ticket.PosterId);
            if(fresh.Fingerprint!=confirmation)throw new IOException("海报或内容已变化，请重新查看恢复预览。");
            var receipt=current.Receipt??new(LocalTransferFiles.Identity(current.File.Destination),LocalTransferFiles.TreeHash(current.File.Objects));
            // Provisional FileVerified and the final index/receipt are in one transaction.
            // A rejected edit or SQL failure rolls all of them back to the original pending row.
            using(var verified=Command(c,t,"UPDATE content_transfer_items SET phase=3,receipt_json=$receipt WHERE operation_id=$id AND item_index=$i",
                ("$receipt",Encode(receipt)),("$id",current.OperationId.ToString()),("$i",current.Index)))verified.ExecuteNonQuery();
            var result=SqliteStateCommandRepository.ExecuteInTransaction(c,t,
                new(ticket.RequestId,confirmation,state.Revision,ticket.Batch.Reads),
                before=>IndexPlan(before,ticket.Batch,current.Index,confirmedRecovery:true),authorizedTransferId:current.OperationId);
            if(result.Status is not (StateCommandStatus.Committed or StateCommandStatus.Replayed))throw new IOException(result.Error??"恢复未提交，文件保持原位。");
            if(LocalTransferFiles.Exists(current.File.Source))throw new IOException("原位置出现了文件，本次恢复未保存；两处均保留。");
            if(!LocalTransferFiles.VerifyDestination(current.File,receipt))throw new IOException("目标内容已变化，本次恢复未保存；文件保持原位。");
            string note=$"已确认恢复记录；文件未再次移动。目标海报：{(ticket.IsIncoming?ticket.PosterId:"不新增海报或卡片")}；确认：{confirmation}";
            using var done=Command(c,t,"UPDATE content_transfer_items SET phase=4,error=$note WHERE operation_id=$id AND item_index=$i",
                ("$note",note),("$id",current.OperationId.ToString()),("$i",current.Index));done.ExecuteNonQuery();
            // Do not rebase sibling items or rewrite the original transfer/confirmation history.
            return result;
        });
    }

    private static IDisposable LockMovedResult(DurableTransferItem row)
    {
        if(Terminal(row)||row.Phase==DurableTransferPhase.Intent)
            throw new IOException("该记录不是已移动待恢复项。尚未开始的收纳可选择重试。");
        if(LocalTransferFiles.Exists(row.File.Source))throw new IOException("原位置仍有文件，请保留两处；本次不能自动恢复关联或删除文件。");
        return LocalTransferFiles.LockVerifiedDestination(row.File,row.Receipt);
    }

    private TransferIndexRecoveryTicket BuildIndexRecovery(DurableTransferItem row,DurableTransferBatch original,
        DesktopStateSnapshot state,string? selectedPosterId)
    {
        var old=original.Entries[row.Index];
        var item=state.ContentCatalog.Items.SingleOrDefault(x=>x.Id==old.Entry.Item.Id)??old.Entry.Item;
        if(item.Source.ProviderId!="local-file-reference-v1" || !string.Equals(item.Target.Locator,row.File.Source,StringComparison.OrdinalIgnoreCase))
            throw new IOException("该内容的来源已另行修改，不能覆盖你的修复。文件位置仍可查看。");
        bool incoming=original.Request.Route==ContentDropRoute.IntoManagedStorage;
        string posterId=incoming ? selectedPosterId??original.Request.PosterId : original.Request.PosterId;
        if(incoming)
        {
            if(!Snapshot(state,posterId).PosterAcceptsTransfer)throw new IOException("请选择一张存在、显示且未锁定的海报。不会重建已删除海报。");
            var collection=state.ContentCatalog.Collections.SingleOrDefault(x=>x.Id==original.StorageCollectionId)
                ??throw new IOException("收纳目录登记缺失；保留文件与记录。");
            ManagedStorageProvisioner.ValidateRegistration(store,original.Request.Root!,collection);
            if(!LocalTransferFiles.Contains(collection.StorageLocator!,row.File.Destination))throw new IOException("实际目标不在原收纳集合中。");
        }
        // Current references, not the obsolete poster set: retain independent repairs, icons,
        // layout and deleted relationships. Only aliases still pointing at the old path move.
        var affected=state.ContentCatalog.Items.Where(x=>x.Source.ProviderId=="local-file-reference-v1" &&
            (string.Equals(x.Target.Locator,row.File.Source,StringComparison.OrdinalIgnoreCase)||LocalTransferFiles.Contains(row.File.Source,x.Target.Locator)))
            .Select(x=>x.Id).Append(item.Id).Distinct().Order().ToArray();
        var members=state.ContentCatalog.Memberships.Where(m=>affected.Contains(m.ContentItemId)).Select(m=>m.Id).ToHashSet();
        var posters=state.ContentCatalog.PosterItems.Where(p=>members.Contains(p.MembershipId)).Select(p=>p.PosterId).Distinct().Order().ToArray();
        var required=posters.Concat(incoming?[posterId]:Array.Empty<string>()).Distinct().ToArray();
        var reads=new StateReadSet(required.ToDictionary(id=>id,id=>state.Catalog.Posters.Single(x=>x.Poster.PosterId==id).EntityVersion),
            CatalogVersion:state.Catalog.CatalogVersion,ContentVersion:state.ContentVersion).Freeze();
        var entries=original.Entries.ToArray();
        entries[row.Index]=old with {Entry=old.Entry with {Item=item},AffectedContentIds=Array.AsReadOnly(affected),AffectedPosterIds=Array.AsReadOnly(posters)};
        var batch=original with {Request=original.Request with {PosterId=posterId},Entries=entries,Reads=reads,Revision=state.Revision};
        var mutation=IndexPlan(state,batch,row.Index,confirmedRecovery:true);
        if(mutation.Rejection is not null)throw new IOException(mutation.Error);
        // Mutation may allocate relation IDs; the confirmation binds inputs, not random output IDs.
        string fingerprint=StateRequestFingerprint.Compute(new {Original=StateRequestFingerprint.Compute(row),posterId,reads,affected,Item=item});
        return new(row,batch,fingerprint);
    }
}
