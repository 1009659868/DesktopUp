using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private Border? _locateOutline;
    private DispatcherQueueTimer? _locateTimer;
    private IDisposable? _locateLease;
    private ResolvedPosterPresentation? _centerPresentation;
    private long _centerPresentationVersion;
    private DesktopPixelRect? _centerBounds;
    private DesktopRecoveryGenerationPair? _centerGenerations;
    internal DesktopPixelRect? CenterPresentationBaseline => _centerBounds is null ? _bounds :
        _centerGenerations == CurrentGenerationPair && _centerPresentationVersion == _posterEntityVersion ? _centerBounds : null;

    internal void AddLocateOutline(Grid root)
    {
        _locateOutline = new Border { IsHitTestVisible = false, Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(2), Margin = new Thickness(2), CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DeepSkyBlue) };
        root.Children.Add(_locateOutline);
    }
    internal bool ShowLocate()
    {
        StopLocate();
        if (_disposed || _window is null || _locateOutline is null || _authoritativePoster?.IsVisible != true) return false;
        _locateOutline.Visibility = Visibility.Visible;
        _locateLease = ledger.Acquire("timer.poster-locate", PosterId!);
        _locateTimer = _window.DispatcherQueue.CreateTimer();
        _locateTimer.Interval = TimeSpan.FromMilliseconds(1400); _locateTimer.IsRepeating = false;
        _locateTimer.Tick += LocateFinished; _locateTimer.Start();
        return true;
    }
    private void LocateFinished(DispatcherQueueTimer sender, object args) => StopLocate();
    internal void StopLocate()
    {
        if (_locateTimer is not null) { _locateTimer.Stop(); _locateTimer.Tick -= LocateFinished; _locateTimer = null; }
        _locateLease?.Dispose(); _locateLease = null;
        if (_locateOutline is not null) _locateOutline.Visibility = Visibility.Collapsed;
    }
    internal string? ValidateCenterPresentation(PosterSettingEdit edit, DesktopPixelRect? bounds = null)
    {
        if (_disposed || _window is null || _contentSession.ActiveCommand is not null || edit.Basis.EntityVersion != _posterEntityVersion ||
            edit.PosterId != PosterId || CenterPresentationBaseline is null) return "海报版本或宿主已变化，请重新开始这次调节。";
        try
        {
            var next = PosterPresentationPolicy.Resolve(edit.Command("validate-preview-only").DesiredState);
            if (edit.Setting is not (PosterSetting.Scale or PosterSetting.Spacing or PosterSetting.Content or PosterSetting.Template)) return null;
            var preferences = next.Parameters.Preferences ?? new();
            ContentLayoutCommandPolicy.ValidateAuthoring(ContentDocument, preferences.Scale, preferences.Spacing);
            var frame = bounds ?? _bounds;
            if (_authoritativePoster?.IsVisible == true && !_geometry.IsCollapsed && _monitor is not null && frame.IsUsable)
            {
                var currentArea = CanvasPosterGeometry.Content(_presentation, frame.Width / _monitor.ScaleX, frame.Height / _monitor.ScaleY);
                var nextArea = CanvasPosterGeometry.Content(next, frame.Width / _monitor.ScaleX, frame.Height / _monitor.ScaleY);
                RequireWritableContentProjection(EffectiveContentDocument, ContentViewportFor(frame).WithPresentation(next.Parameters) with
                    {
                      WidthDip = Math.Max(0, ContentViewportFor(frame).WidthDip + nextArea.Width - currentArea.Width),
                      HeightDip = Math.Max(0, ContentViewportFor(frame).HeightDip + nextArea.Height - currentArea.Height) });
            }
            return null;
        }
        catch (ArgumentException error) { return error.Message; }
    }
    internal bool PreviewCenterPresentation(PosterSettingEdit edit, DesktopPixelRect? bounds = null)
    {
        if (ValidateCenterPresentation(edit, bounds) is not null) return false;
        return PreviewCenterPresentation(edit.Command("visual-preview-only"), bounds ?? _bounds);
    }
    internal bool PreviewCenterPresentation(PosterStateCommand command, DesktopPixelRect bounds)
    {
        if (_disposed || _authoritativePoster?.IsVisible != true || CenterPresentationBaseline is null ||
            command.ReadSet?.Posters.GetValueOrDefault(PosterId!) != _posterEntityVersion) return false;
        _centerBounds ??= _bounds;
        _centerGenerations ??= CurrentGenerationPair;
        _centerPresentation = PosterPresentationPolicy.Resolve(command.DesiredState);
        _centerPresentationVersion = _posterEntityVersion;
        ApplyPresentation(_centerPresentation);
        // Native bounds and content/input are published together; no persisted revision yet.
        if (ApplyFrame(bounds)) return true;
        ClearCenterPresentation();
        return false;
    }
    internal void ClearCenterPresentation()
    {
        if (_centerPresentation is null && _centerBounds is null) return;
        var restore = CenterPresentationBaseline ?? _bounds;
        _centerPresentation = null;
        _centerBounds = null; _centerGenerations = null;
        if (!_disposed && _window is not null && _authoritativePoster is not null)
        {
            ApplyPresentation(PosterPresentationPolicy.Resolve(_authoritativePoster));
            if (!ApplyFrame(restore)) spatial.CenterPresentationRestoreFailed(this);
        }
    }
}
