using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private string? BeginDirectoryMapping()
    {
        if (_disposed || HasFileAction || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!)) return "请先完成当前操作。";
        SetFileActionBusy(true); spatial.TrackContentOperation(Observe()); return null;
        async Task Observe()
        {
            try
            {
                var picker = new NativeProfilePicker(_windowHandle, false, ledger, log,
                    initiallyCancelled: _contentOpenCancellation.IsCancellationRequested, purpose: NativePickerPurpose.MappedDirectory);
                using var stop = _contentOpenCancellation.Token.Register(picker.Cancel);
                string? path = await picker.Completion;
                if (path is not null && !_disposed) await spatial.CreateMappedPoster(path, _contentOpenCancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!_disposed) await ShowFileActionError("未建立目录映射：" + e.Message); }
            finally { SetFileActionBusy(false); if (!_disposed) QueueContentProposalDrain(); }
        }
    }
}
