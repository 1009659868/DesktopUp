using DesktopUp.Infrastructure;
using DesktopUp.Domain;
using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private SqliteStateStore? _transferStore;
    private Func<Task<RegisteredManagedStorage?>>? _chooseTransferStorage;
    private Func<string,Task<bool>>? _confirmTransfer;
    private Func<bool>? _transferDialogsBusy;
    private Action<string>? _showTransferFailure;
    private Task _transferStartup=Task.CompletedTask;
    private readonly CancellationTokenSource _fileTransferStopping=new();
    private Task _fileTransferTask=Task.CompletedTask;
    private bool _fileTransferBusy;
    private string? _fileTransferPoster;
    internal bool IsFileTransferActive(string id)=>_fileTransferBusy&&_fileTransferPoster==id;
    internal bool TransfersAvailable => _transferStore is not null && _transferStartup.IsCompletedSuccessfully &&
        !_fileTransferStopping.IsCancellationRequested && !_fileTransferBusy && !_suspended && _transferDialogsBusy?.Invoke()!=true;
    internal void ConfigureTransfers(SqliteStateStore store,Task startup,Func<Task<RegisteredManagedStorage?>> choose,
        Func<string,Task<bool>> confirm,Func<bool> dialogsBusy,Action<string> failure)
    { _transferStore=store;_transferStartup=startup;_chooseTransferStorage=choose;_confirmTransfer=confirm;_transferDialogsBusy=dialogsBusy;_showTransferFailure=failure; }
    internal Task StopTransfersAsync()
    { _fileTransferStopping.Cancel();return _fileTransferTask; }

    internal void TransferFiles(PosterVisualWindow window,string[]? paths,string[]? layoutIds)
    {
        // Old deterministic executor is now restricted to the owned-data regression probe.
        // Production mouse egress must enter NativeShellDragSource, never this code path.
        if(paths is null && _outgoingProbeDestination is null)return;
        if(!TransfersAvailable || !window.CanTransferFiles)return;
        _fileTransferBusy=true;
        _fileTransferPoster=window.PosterId;ActivityChanged?.Invoke();
        _fileTransferTask=paths is null?RunOutgoing(window,layoutIds??[]):RunTransfer(window,paths,layoutIds);
    }
    private async Task RunTransfer(PosterVisualWindow window,string[]? paths,string[]? layoutIds)
    {
        string id=window.PosterId!;
        long entity=window.TransferEntityVersion;
        bool Current()=>!_fileTransferStopping.IsCancellationRequested && !_suspended && window.CanTransferFiles &&
            window.TransferEntityVersion==entity && _windows.GetValueOrDefault(id)==window && !HasCenterDraft(id);
        try
        {
            var journal=new SqliteTransferJournal(_transferStore!);
            RegisteredManagedStorage? storage=null;
            if(paths is not null)
            {
                storage=await _chooseTransferStorage!();
                if(storage is null)return;
            }
            if(!Current())return;
            window.TransferFeedback("正在检查文件…");
            var preview=await Task.Run(()=>
            {
                var snapshot=new SqliteStateCommandRepository(_transferStore!).Read();
                return paths is not null?DirectTransferPreparation.Incoming(snapshot,id,paths,storage!):
                    DirectTransferPreparation.Outgoing(snapshot,id,layoutIds!);
            });
            if(!Current())return;
            if(preview.Plan.RequiresConfirmation)
            {
                string detail=string.Join("\n\n",preview.Files.Select(f=>$"原位置（将移出）：{f.Source}\n目标：{f.Destination}"));
                string posters=string.Join("、",preview.Plan.Entries.SelectMany(x=>x.AffectedPosterIds).Append(id).Distinct());
                if(!await _confirmTransfer!(detail+"\n\n影响海报："+posters+
                    "\n只移动文件或快捷方式本体。首次收纳会替换当前海报演示内容；取出仅移除当前卡片，其他引用同步更新位置。"))return;
            }
            if(!Current())return;
            window.TransferFeedback("正在移动文件…");
            var result=await Task.Run(()=>
            {
                _fileTransferStopping.Token.ThrowIfCancellationRequested();
                journal.Begin(preview.Plan,preview.Files,preview.Plan.Fingerprint,storage?.Collection.Id);
                return new AutomaticTransferCompletion(journal).Execute(preview.Plan.Request.OperationId,_fileTransferStopping.Token);
            });
            state.Refresh();
            int moved=result.Count(x=>x.Kind==TransferFinishKind.Completed);
            int preserved=result.Count(x=>x.Kind==TransferFinishKind.Preserved);
            if(preserved>0&&!_fileTransferStopping.IsCancellationRequested)
                _showTransferFailure?.Invoke("未删除或覆盖任何不确定文件。可查看异常文件位置，再重新拖动。");
            window.TransferFeedback(preserved>0?$"完成{moved}项；部分结果未确认，文件保留，可在“异常文件位置”查看。":
                moved==result.Count?$"已{(paths is null?"取回桌面":"收纳")} {moved} 项。":
                $"完成{moved}项，其余未移动；可直接重新拖动。");
            log.Write("info","transfer.direct_finished",new{posterId=id,operation=preview.Plan.Request.OperationId,moved,preserved,total=result.Count});
        }
        catch(OperationCanceledException){window.TransferFeedback("已取消；可重新拖动。");}
        catch(Exception e)
        {
            // The executor settles ordinary failures; preflight failures have not moved files.
            window.TransferFeedback("本次未能完成，可重新拖动："+e.Message);
            if(!_fileTransferStopping.IsCancellationRequested)_showTransferFailure?.Invoke(e.Message+"\n可直接重新拖动，无需继续旧任务。");
            log.Write("warning","transfer.direct_failed",new{posterId=id,e.Message});
            state.Refresh();
        }
        finally{_fileTransferBusy=false;_fileTransferPoster=null;ActivityChanged?.Invoke();}
    }
}
