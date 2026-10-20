using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private ContentOperationLifetime.FileOperation? _nativeExitOperation;
    private bool _nativeExitCompleting;
    private bool _nativeExitToPoster;
    private ShellExitIntent? _nativeExitIntent;
    internal bool CanReceiveInto(string id)=>TransfersAvailable || _nativeExitOperation is not null && !IsFileTransferActive(id) &&
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
        var operation = BeginFileOperation(window.PosterId!);
        try
        {
            var intent = _nativeShellExit!.Begin(window.PosterId!, selection);
            _nativeExitIntent = intent;
            _nativeExitOperation = operation;
            return intent;
        }
        catch { operation.Dispose(); throw; }
    }
    internal bool NativeExitCurrent => !ContentOperationsStopping && !_suspended;
    internal CancellationToken NativeExitStopping => _contentOperations.Stopping;
    internal void CompleteNativeExit(PosterVisualWindow window, ShellExitIntent intent, NativeShellDragResult? result)
    {
        if (_nativeExitOperation is not { } operation || _nativeExitIntent?.Id != intent.Id || _nativeExitCompleting) return;
        _nativeExitCompleting = true;
        TrackContentOperation(ObserveNativeExit(window, intent, result, operation));
    }

    private async Task ObserveNativeExit(PosterVisualWindow window, ShellExitIntent intent,
        NativeShellDragResult? result, ContentOperationLifetime.FileOperation operation)
    {
        try
        {
            await _nativeShellExit!.Complete(intent,
                result is null ? null : new ShellExitCompletion(result.Released, result.ReturnedToOrigin, result.Effect, _nativeExitToPoster),
                new NativeExitPresentation(() => state.Refresh(), (paths, token) =>
                    window.ConfirmMissingReferenceAsync("以下原位置已不存在。仅从海报移除失效卡片，不删除任何文件：\n\n"+string.Join("\n",paths),token)),
                _contentOperations.Stopping);
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
                _nativeExitOperation=null; _nativeExitToPoster=false; _nativeExitIntent=null; _nativeExitCompleting=false;
                operation.Dispose();
                ActivityChanged?.Invoke();
            }
        }
    }
}
