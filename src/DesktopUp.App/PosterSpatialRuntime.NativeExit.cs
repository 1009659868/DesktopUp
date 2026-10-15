using DesktopUp.Infrastructure;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private TaskCompletionSource? _nativeExitLifetime;
    private bool _nativeExitToPoster;
    private ShellExitIntent? _nativeExitIntent;
    internal bool CanReceiveInto(string id)=>TransfersAvailable || _nativeExitLifetime is not null && _fileTransferPoster!=id &&
        !_nativeExitToPoster && NativeExitCurrent;
    internal ShellExitIntent? PreparePosterDropHandoff(string id, IReadOnlyList<string> paths)
    {
        // OLE Drop must mark this before DoDragDrop unwinds; queued UI work is too late.
        if(!TransfersAvailable && CanReceiveInto(id) && _nativeExitIntent is { } intent &&
            intent.Items.Where(i=>!i.Missing).Select(i=>i.Path).SequenceEqual(paths,StringComparer.OrdinalIgnoreCase))
        { _nativeExitToPoster=true; return intent; }
        return null;
    }
    internal ShellExitIntent? BeginNativeExit(PosterVisualWindow window, string[] selection)
    {
        if (!TransfersAvailable) return null;
        // Called while the original button is still down. Durable intent precedes handing
        // physical paths to Shell; unlike the old path there is no proposed destination.
        var intent = new NativeShellExitJournal(_transferStore!).Begin(window.PosterId!, selection);
        _fileTransferBusy = true; _fileTransferPoster = window.PosterId;
        _nativeExitIntent = intent;
        _nativeExitLifetime = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _fileTransferTask = _nativeExitLifetime.Task; ActivityChanged?.Invoke(); return intent;
    }
    internal bool NativeExitCurrent => !_fileTransferStopping.IsCancellationRequested && !_suspended;
    internal CancellationToken NativeExitStopping => _fileTransferStopping.Token;
    internal async void CompleteNativeExit(PosterVisualWindow window, ShellExitIntent intent, NativeShellDragResult? result)
    {
        var lifetime = _nativeExitLifetime;
        try
        {
            var journal = new NativeShellExitJournal(_transferStore!);
            bool external = !_nativeExitToPoster && result is { Released:true, ReturnedToOrigin:false };
            journal.End(intent, external);
            if (external)
            {
                // MOVE/NONE may both mean optimized or asynchronous Shell movement. Prove
                // source absence per item; cancelled/rejected/copy items remain on the poster.
                for (int attempt=0; attempt<6 && !_fileTransferStopping.IsCancellationRequested; attempt++)
                {
                    await Task.Delay(attempt==0?150:500,_fileTransferStopping.Token);
                    int removed = await Task.Run(()=>journal.ObserveReleasedMove(intent));
                    if (removed>0) state.Refresh();
                    var snapshot=new SqliteStateCommandRepository(_transferStore!).Read();
                    if (intent.Items.Where(i=>!i.Missing).All(i=>snapshot.ContentCatalog.PosterItems.All(p=>p.Id!=i.CardId)) ||
                        result!.Effect is 1 or 4) break;
                }
                if (intent.Items.Any(i=>i.Missing) && !_fileTransferStopping.IsCancellationRequested)
                {
                    string list=string.Join("\n",intent.Items.Where(i=>i.Missing).Select(i=>i.Path));
                    if (await window.ConfirmMissingReferenceAsync("以下原位置已不存在。仅从海报移除失效卡片，不删除任何文件：\n\n"+list,_fileTransferStopping.Token))
                        await Task.Run(()=>journal.RemoveStaleReferencesAfterUserConfirmation(intent));
                }
                journal.Reconcile();
            }
            log.Write("info","transfer.native_shell_returned",new{intent.Id,result?.Released,result?.Effect,result?.ReturnedToOrigin,applicationFileActions=false});
        }
        catch(OperationCanceledException) { }
        catch(Exception e) { log.Write("warning","transfer.native_shell_observation_deferred",new{intent.Id,e.Message}); }
        finally
        {
            try { state.Refresh(); }
            catch(Exception e) { log.Write("warning","transfer.native_shell_refresh_deferred",new{e.Message}); }
            finally
            {
                _fileTransferBusy=false; _fileTransferPoster=null;
                _nativeExitLifetime=null; _nativeExitToPoster=false; _nativeExitIntent=null; lifetime?.TrySetResult(); ActivityChanged?.Invoke();
            }
        }
    }
}
