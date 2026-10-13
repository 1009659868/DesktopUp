using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private DispatcherQueueTimer? _contentFeedbackTimer;
    private IDisposable? _contentFeedbackLease;
    private string? _contentTimedFeedback;
    private string? _contentFeedbackFull, _contentFeedbackBrief;

    private void AddContentFeedback(Grid structure) => RenderContentFeedback();

    private void RenderContentFeedback()
    {
        RenderContentSpaceNavigation();
        if (_footer is not null) _footer.Visibility = Visibility.Collapsed;
    }

    private void SetContentImpact(ContentInteractionFeedback feedback)
    {
        _contentFeedbackFull = _contentNotice = feedback.Message;
        var impact = feedback.Impact;
        bool preview = feedback.State == ContentFeedbackState.Preview;
        string end = preview ? "Esc取消" : "可撤销";
        _contentFeedbackBrief = impact.AdoptsProjection
            ? (preview ? "松手采用当前整张构图" : "已采用当前整张构图") +
                $"\n屏外{impact.OffscreenAffectedItemIds.Count}·让位{impact.DisplacedItemIds.Count}  {end}"
            : impact.EnteredManual ? (preview ? "将按眼前整张排列进入手动" : "已按眼前整张排列进入手动") + "\n保留原自动设置 · 可撤销"
            : impact.RestoredAutomatic ? "已恢复原自动排布\n图钉/尺寸恢复 · 可撤销"
            : impact.FinalPinnedPosition is { } pin ? (preview ? "松手固定" : "已固定") + $"到第 {pin} 位\n其余自动排序 · {end}"
            : impact.DisplacedItemIds.Count > 0 ? (preview ? "松手保存调整" : "调整已保存") + $"\n屏外{impact.OffscreenAffectedItemIds.Count}·让位{impact.DisplacedItemIds.Count}  {end}"
            : ContentFeedbackForDisplay(feedback.Message);
    }

    private void PublishContentImpact(ContentInteractionFeedback feedback)
    {
        SetContentImpact(feedback);
        PublishContentFeedback(feedback.Message, transient: true);
    }

    private void PublishContentFeedback(string? message, bool transient = false)
    {
        StopContentFeedbackTimer();
        _contentNotice = message;
        _contentTimedFeedback = transient ? message : null;
        RenderContentFeedback();
        spatial.NotifyContentEditorChanged(PosterId);
        if (!transient || message is null || _disposed || _contentPresentationSuspended || _window is null) return;
        _contentFeedbackLease = ledger.Acquire("timer.content-feedback", PosterId ?? inputSession.PosterId);
        _contentFeedbackTimer = _window.DispatcherQueue.CreateTimer();
        _contentFeedbackTimer.Interval = TimeSpan.FromSeconds(5);
        _contentFeedbackTimer.IsRepeating = false;
        _contentFeedbackTimer.Tick += ContentFeedbackElapsed;
        _contentFeedbackTimer.Start();
    }

    private void ContentFeedbackElapsed(DispatcherQueueTimer timer, object args)
    {
        if (!ReferenceEquals(timer, _contentFeedbackTimer)) return;
        string? expiring = _contentTimedFeedback;
        StopContentFeedbackTimer();
        // A later read-only/error state may have replaced the message without a user action.
        // A result timer must never erase that newer explanation or a live drag preview.
        if (_contentNotice == expiring && !HasContentGesture)
        {
            _contentNotice = null;
            RenderContentFeedback();
            spatial.NotifyContentEditorChanged(PosterId);
        }
    }

    private void StopContentFeedbackTimer()
    {
        if (_contentFeedbackTimer is not null)
        {
            _contentFeedbackTimer.Stop();
            _contentFeedbackTimer.Tick -= ContentFeedbackElapsed;
            _contentFeedbackTimer = null;
        }
        _contentFeedbackLease?.Dispose(); _contentFeedbackLease = null; _contentTimedFeedback = null;
    }

    private void SuspendContentFeedback()
    {
        if (_contentTimedFeedback is not null && _contentNotice == _contentTimedFeedback) _contentNotice = null;
        StopContentFeedbackTimer();
        RenderContentFeedback();
    }

    private string ReportContentFailure(string message)
    {
        PublishContentFeedback(message);
        return message;
    }

    private static string? ContentFeedbackForDisplay(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        // Keep the original reason for probes/diagnostics, without exposing solver exceptions
        // as the user's only explanation of an unsuccessful operation.
        if (message.Contains("No following order-anchor slot", StringComparison.Ordinal))
            return "这个顺位及其后的位置都已固定。请换一个位置；本次没有保存。";
        if (message.Contains("Original automatic intent is unknown", StringComparison.Ordinal))
            return "这份旧手动构图没有可恢复的自动设置，当前构图已保留。";
        if (message.Contains("original automatic size basis is unknown", StringComparison.OrdinalIgnoreCase))
            return "这张卡片没有原自动尺寸记录，当前大小已保留。";
        if (message.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("reserved space", StringComparison.OrdinalIgnoreCase))
            return "这个位置与已有内容、保留区域或当前空间冲突。请换一个位置；本次没有保存。";
        if (message.Contains("Loop", StringComparison.OrdinalIgnoreCase))
            return "循环浏览中的拖动尚未开放。当前排列已保留。";
        if (message.All(c => c <= 127))
            return "这次调整未能完成，原布局已保留。可换一个位置或取消后重试。";
        return message;
    }

    private void ClearContentFeedbackVisuals()
    {
        StopContentFeedbackTimer();
    }
}
