using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp;
internal sealed partial class PosterSpatialRuntime
{
    private async Task RunOutgoing(PosterVisualWindow window,string[] selection)
    {
        string id=window.PosterId!;long entity=window.TransferEntityVersion;
        int completed=0,failed=0;var errors=new List<string>();
        bool Current()=>!_fileTransferStopping.IsCancellationRequested&&!_suspended&&window.CanTransferFiles&&
            window.TransferEntityVersion==entity&&_windows.GetValueOrDefault(id)==window&&!HasCenterDraft(id);
        try
        {
            var repository=new SqliteStateCommandRepository(_transferStore!);
            var initial=repository.Read();
            var sources=DirectTransferPreparation.OutgoingSources(initial,id,selection);
            var journal=new SqliteTransferJournal(_transferStore!);
            foreach(var source in sources)
            {
                if(!Current()){errors.Add("环境已变化，剩余项未移动。");break;}
                try
                {
                    window.TransferFeedback($"正在取出 {Path.GetFileName(source.Locator)}…（已完成 {completed} 项）");
                    var preview=await Task.Run(()=>
                    {
                        var current=repository.Read();
                        if(DirectTransferPreparation.OutgoingSources(current,id,[source.LayoutId]).Single()!=source)
                            throw new IOException("内容来源已变化，请重新拖动。");
                        return PrepareOutgoingForRuntime(current,id,source.LayoutId);
                    });
                    if(!Current()){errors.Add("海报已变化，剩余项未移动。");break;}
                    if(preview.Plan.RequiresConfirmation)
                    {
                        string message=$"原位置（将移出）：\n{preview.Files[0].Source}\n\n目标：\n{preview.Files[0].Destination}\n\n影响海报："+
                            string.Join("、",preview.Plan.Entries.SelectMany(e=>e.AffectedPosterIds).Append(id).Distinct())+
                            "\n只移动对象本身；仅移除当前卡片，其他引用更新位置。";
                        if(!await window.ConfirmOutgoingAsync(message,_fileTransferStopping.Token)||!Current())
                        {errors.Add("已取消，剩余项未移动。");break;}
                    }
                    var result=await Task.Run(()=>
                    {
                        _fileTransferStopping.Token.ThrowIfCancellationRequested();
                        journal.Begin(preview.Plan,preview.Files,preview.Plan.Fingerprint);
                        return new AutomaticTransferCompletion(journal).Execute(preview.Plan.Request.OperationId,_fileTransferStopping.Token);
                    });
                    state.Refresh();entity=window.TransferEntityVersion;
                    if(result.Single().Item.Phase==DurableTransferPhase.NoOp)
                    {errors.Add($"{Path.GetFileName(source.Locator)} 已在桌面，未移动，卡片保持。");}
                    else if(result.Single().Kind==TransferFinishKind.Completed)completed++;
                    else {failed++;errors.Add($"{Path.GetFileName(source.Locator)}：未完成；文件保留。" );}
                }
                catch(OperationCanceledException){errors.Add("已取消，剩余项未移动。");break;}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {failed++;errors.Add($"{Path.GetFileName(source.Locator)}：{e.Message}");}
            }
            window.TransferFeedback(errors.Count==0?$"已取出 {completed} 项。":$"已取出 {completed} 项；"+string.Join("；",errors.Take(3)));
            log.Write("info","transfer.egress_finished",new{posterId=id,completed,failed,total=sources.Count,errors});
        }
        catch(Exception e){window.TransferFeedback("本次未移动，请重新拖动："+e.Message);log.Write("warning","transfer.egress_failed",new{e.Message});}
        finally{state.Refresh();_fileTransferBusy=false;_fileTransferPoster=null;ActivityChanged?.Invoke();}
    }
    private string? _outgoingProbeDestination = null; // Assigned only by the guarded owned-data probe.
    private LocalTransferPreview PrepareOutgoingForRuntime(DesktopStateSnapshot state,string poster,string layoutId)
    {
        if(_outgoingProbeDestination is null)return DirectTransferPreparation.Outgoing(state,poster,[layoutId]);
        var relation=ContentRelationPolicy.Resolve(state.ContentCatalog,poster,layoutId)!;
        return LocalTransferPreview.Prepare(state,poster,ContentDropRoute.ToDesktop,
            [(relation.Item,Path.Combine(_outgoingProbeDestination,Path.GetFileName(relation.Item.Target.Locator)),relation.PosterItem.Id)],
            isolatedDestination:true,keepSourceUntilCommit:true);
    }
}
