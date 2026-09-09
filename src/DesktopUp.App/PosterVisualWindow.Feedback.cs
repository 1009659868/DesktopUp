using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private readonly ExpansionFeedback _expansionFeedback = new();
    private DispatcherQueueTimer? _feedbackTimer;
    private Brush? _feedbackOriginalBrush;
    private readonly TranslateTransform _feedbackTransform = new();

    public void ShowInsufficientSpaceFeedback()
    {
        if (_disposed || _window is null || _collapseGlyph is null) return;
        bool animate;
        try { animate = new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled; }
        catch { animate = false; } // A missing settings service must not break a native input callback.
        if (spatial.TryGetProjection(out var projection) && projection?.ControlCenter.Settings.Settings.DynamicEffectsPaused == true) animate = false;
        if (!_expansionFeedback.TryStart(Environment.TickCount64, animate)) return;
        _feedbackOriginalBrush = _collapseGlyph.Foreground;
        _collapseGlyph.Foreground = Brush(255, 244, 182, 83);
        _collapseGlyph.RenderTransform = _feedbackTransform;
        _feedbackTimer ??= _window.DispatcherQueue.CreateTimer();
        _feedbackTimer.Interval = TimeSpan.FromMilliseconds(16);
        _feedbackTimer.Tick -= AdvanceExpansionFeedback;
        _feedbackTimer.Tick += AdvanceExpansionFeedback;
        _feedbackTimer.Start();
        log.Write("info", "spatial.expansion_feedback", new { PosterId, animate, durationMs = ExpansionFeedback.DurationMilliseconds, bounds = _bounds });
    }
    private void AdvanceExpansionFeedback(DispatcherQueueTimer timer, object args)
    {
        var sample = _expansionFeedback.Sample(Environment.TickCount64);
        _feedbackTransform.X = sample.OffsetDip;
        if (!sample.Active) StopExpansionFeedback();
    }
    private void StopExpansionFeedback()
    {
        if (_feedbackTimer is not null) { _feedbackTimer.Stop(); _feedbackTimer.Tick -= AdvanceExpansionFeedback; _feedbackTimer = null; }
        _expansionFeedback.Reset(); _feedbackTransform.X = 0;
        if (_collapseGlyph is not null && _feedbackOriginalBrush is not null) _collapseGlyph.Foreground = _feedbackOriginalBrush;
        _feedbackOriginalBrush = null;
    }
}
