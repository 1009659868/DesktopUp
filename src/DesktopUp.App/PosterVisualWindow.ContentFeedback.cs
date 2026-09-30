using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // An isolated data directory is also used for human review. It must not opt the user into
    // diagnostics. Only a test runner that explicitly sets this child-process flag shows them.
    private static readonly bool TestContentFeedbackEnabled = string.Equals(
        Environment.GetEnvironmentVariable("DESKTOPUP_TEST_CONTENT_FEEDBACK"), "1", StringComparison.Ordinal);
    private Border? _contentFeedbackCallout;
    private TextBlock? _contentFeedbackText;
    private DispatcherQueueTimer? _contentFeedbackTimer;
    private IDisposable? _contentFeedbackLease;
    private string? _contentTimedFeedback;
    private string? _contentFeedbackFull, _contentFeedbackBrief;

    // Use the existing footer chrome, outside every Item/handle/viewport hit region.
    // A drawing-only toast over the cards would leave invisible cards clickable beneath it.
    private void AddContentFeedback(Grid structure)
    {
        if (!TestContentFeedbackEnabled)
        {
            RenderContentFeedback();
            return;
        }
        _contentFeedbackText = new TextBlock
        {
            FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 16, Height = 34,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = ColorBrush("#F3F1E9"), IsHitTestVisible = false
        };
        _contentFeedbackCallout = new Border
        {
            Child = _contentFeedbackText, Background = ColorBrush("#252B2B", 246),
            BorderBrush = ColorBrush("#BFAE85", 220), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(HorizontalPaddingDip, 1, HorizontalPaddingDip, 1),
            VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch,
            IsHitTestVisible = false, Visibility = Visibility.Collapsed
        };
        Grid.SetRow(_contentFeedbackCallout, 3);
        structure.Children.Add(_contentFeedbackCallout);
        RenderContentFeedback();
    }

    private string ContentModeCaption => _contentDocument?.Mode == ContentLayoutMode.Manual ? "手动排布" : "热度自动";

    private void RenderContentFeedback()
    {
        RenderContentSpaceNavigation();
        if (!TestContentFeedbackEnabled)
        {
            // Preserve the reserved footer geometry so hiding diagnostic text cannot move or
            // enlarge live card hit regions. Center errors and the underlying notice stay intact.
            if (_footer is not null) _footer.Visibility = Visibility.Collapsed;
            if (_contentFeedbackCallout is not null) _contentFeedbackCallout.Visibility = Visibility.Collapsed;
            return;
        }
        if (_modeLabel is not null)
            _modeLabel.Text = ContentModeCaption + (_geometry.IsLocked ? " · 已锁定" :
                _contentReadOnly ? " · 暂不可编辑" : IsEditing ? " · 外框调整" : " · 右键管理");
        if (_contentFeedbackCallout is null || _contentFeedbackText is null) return;
        string? visible = _contentNotice == _contentFeedbackFull && _contentFeedbackBrief is not null
            ? _contentFeedbackBrief : ContentFeedbackForDisplay(_contentNotice);
        _contentFeedbackText.Text = visible ?? "";
        _contentFeedbackCallout.Visibility = !_disposed && !_geometry.IsCollapsed &&
            !_contentPresentationSuspended && visible is not null ? Visibility.Visible : Visibility.Collapsed;
        if (_footer is not null)
            _footer.Visibility = _geometry.IsCollapsed || _contentFeedbackCallout.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
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
        _contentFeedbackCallout = null; _contentFeedbackText = null;
    }
}
