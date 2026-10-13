using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private CancellationTokenSource? _incomingCancellation;
    internal void ReceiveFiles(PosterVisualWindow window,IncomingDropTicket ticket,IncomingContentPlacement? landing)
    {
        if(!TransfersAvailable || !window.CanTransferFiles || !window.IncomingGeometryCurrent(ticket.Preview.Surface))return;
        _fileTransferBusy=true;_fileTransferPoster=window.PosterId;
        _incomingCancellation=CancellationTokenSource.CreateLinkedTokenSource(_fileTransferStopping.Token);
        ActivityChanged?.Invoke();
        _fileTransferTask=RunIncoming(window,ticket,landing,_incomingCancellation);
    }
    private async Task RunIncoming(PosterVisualWindow window,IncomingDropTicket ticket,IncomingContentPlacement? landing,CancellationTokenSource lifetime)
    {
        string id=window.PosterId!;long entity=window.TransferEntityVersion;
        int completed=0,failed=0;string? lastError=null;
        bool Current()=>!lifetime.IsCancellationRequested && !_suspended && _windows.GetValueOrDefault(id)==window &&
            window.IncomingGeometryCurrent(ticket.Preview.Surface) && window.TransferEntityVersion==entity && !HasCenterDraft(id);
        try
        {
            var storage=await _chooseTransferStorage!();
            if(storage is null || !Current())return;
            // Freeze payload order; overlapping parent/child paths cannot become two independent moves.
            var normalized=ticket.Paths.Select(LocalTransferFiles.Normalize).ToArray();
            if(normalized.Any(p=>normalized.Any(other=>p!=other&&LocalTransferFiles.Contains(p,other))))
                throw new IOException("请不要同时选择文件夹及其内部文件；本次未移动。");
            var journal=new SqliteTransferJournal(_transferStore!);
            foreach(var path in normalized)
            {
                if(!Current()){lastError="海报或桌面环境已变化，剩余项未移动。";break;}
                try
                {
                    window.TransferFeedback($"正在收纳 {Path.GetFileName(path)}…（已完成 {completed} 项）");
                    var snapshot=await Task.Run(()=>new SqliteStateCommandRepository(_transferStore!).Read());
                    var preview=await Task.Run(()=>DirectTransferPreparation.Incoming(snapshot,id,[path],storage,landing));
                    if(!Current()){lastError="落点已变化，剩余项未移动。";break;}
                    // DEC-047: the drop itself expresses move intent; no application confirmation.
                    var result=await Task.Run(()=>
                    {
                        lifetime.Token.ThrowIfCancellationRequested();
                        journal.Begin(preview.Plan,preview.Files,preview.Plan.Fingerprint,storage.Collection.Id);
                        return new AutomaticTransferCompletion(journal).Execute(preview.Plan.Request.OperationId,lifetime.Token);
                    });
                    state.Refresh();entity=window.TransferEntityVersion;
                    var finish=result.Single();
                    if(finish.Kind==TransferFinishKind.Completed)
                    {
                        completed++;
                        if(landing is not null)
                        {
                            var committed=new SqliteStateCommandRepository(_transferStore!).Read();
                            var member=committed.ContentCatalog.Memberships.FirstOrDefault(m=>m.ContentItemId==preview.Plan.Entries[0].Entry.Item.Id &&
                                committed.ContentCatalog.Bindings.Any(b=>b.PosterId==id&&b.CollectionId==m.CollectionId));
                            var card=committed.ContentCatalog.PosterItems.FirstOrDefault(p=>p.PosterId==id&&p.MembershipId==member?.Id);
                            var poster=committed.Catalog.Posters.Single(x=>x.Poster.PosterId==id).Poster;
                            if(card is not null && ContentLayoutPolicy.TryRead(poster.ContentLayoutJson,id,out var document,out _))
                                landing=landing.Advance(document!,card.LayoutItemId);
                        }
                    }
                    else {failed++;lastError=finish.Kind==TransferFinishKind.Preserved?$"文件已保留，请查看：{path} 与 {preview.Files[0].Destination}":"该项未移动，可重新拖动。";}
                }
                catch(OperationCanceledException){lastError="已取消，未完成项保留。";break;}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
                {failed++;lastError=$"{Path.GetFileName(path)}：{e.Message}";}
            }
            window.TransferFeedback(lastError is null?$"已收纳 {completed} 项。":$"已收纳 {completed} 项；{lastError}");
            log.Write("info","transfer.ingress_finished",new{posterId=id,completed,failed,total=normalized.Length,lastError});
        }
        catch(Exception e){window.TransferFeedback("本次未能完成，可重新拖动："+e.Message);log.Write("warning","transfer.ingress_failed",new{posterId=id,e.Message});}
        finally
        {
            state.Refresh();_fileTransferBusy=false;_fileTransferPoster=null;
            _incomingCancellation=null;lifetime.Dispose();ActivityChanged?.Invoke();
        }
    }
}
