using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private readonly CancellationTokenSource _contentOpenCancellation = new();
    private long _contentOpenLifetime;
    private bool IsRealContent => _contentPorts.Open is IAsyncContentActions;

    private void DispatchRealContentOpen(ContentOpenIntent intent, IAsyncContentActions actions)
    {
        if (spatial.ContentOperationsStopping) return;
        long lifetime = _contentOpenLifetime;
        var ports = _contentPorts;
        PublishContentFeedback("正在交给 Windows 打开…", transient: true);
        spatial.TrackContentOperation(Observe());
        async Task Observe()
        {
            var lease = ledger.Acquire("content.open:" + intent.ActivationId, intent.Target.ItemId);
            try
            {
                try { await spatial.ValidateMappedAction(PosterId!, intent.Target.ItemId); }
                catch (IOException e)
                {
                    if (!_disposed) await ShowFileActionError(e.Message);
                    return;
                }
                var result = await Task.Run(async () => await actions.OpenAsync(intent, _contentOpenCancellation.Token));
                if (!_disposed && lifetime == _contentOpenLifetime && ReferenceEquals(ports, _contentPorts) &&
                    ContentDocument.Placements.Any(p => p.ItemId == intent.Target.ItemId))
                    PublishContentFeedback(result.Message, transient: true);
                log.Write("info", "content.open_completed", new { PosterId, intent.ActivationId, outcome = result.Outcome.ToString(), result.Succeeded });
            }
            catch (Exception e)
            {
                log.Write("warning", "content.open_result_failed", new { PosterId, error = e.GetType().Name });
                if (!_disposed && lifetime == _contentOpenLifetime)
                    PublishContentFeedback("未能记录打开结果；不自动重试，请核对外部程序。", transient: true);
            }
            finally { lease.Dispose(); }
        }
    }

    internal string? RevealSelectedContent()
    {
        if (_contentPorts.Open is not IAsyncContentActions actions || _selectedContentItem is not { } item) return "请选择真实内容。";
        if (spatial.ContentOperationsStopping || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!)) return "请先完成当前交互。";
        long lifetime = _contentOpenLifetime;
        PublishContentFeedback("正在定位来源…", transient: true);
        spatial.TrackContentOperation(Observe());
        return null;
        async Task Observe()
        {
            using var lease = ledger.Acquire("content.reveal:" + Guid.NewGuid().ToString("N"), item);
            try
            {
                var result = await actions.RevealAsync(item, _contentOpenCancellation.Token);
                if (!_disposed && lifetime == _contentOpenLifetime) PublishContentFeedback(result.Message, transient: true);
            }
            catch (Exception) { if (!_disposed) PublishContentFeedback("暂时无法定位来源。", transient: true); }
        }
    }
    internal string? RefreshRealContent()
    {
        if (_contentPorts.Open is not IAsyncContentActions actions) return "当前仍为 DEMO。";
        actions.Refresh(); return null;
    }
}
