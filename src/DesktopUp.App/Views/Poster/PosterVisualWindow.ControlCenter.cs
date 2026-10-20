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
    internal string? ValidateCenterPresentation(PosterSettingEdit edit)
    {
        if (_disposed || _window is null || _contentSession.ActiveCommand is not null || edit.Basis.EntityVersion != _posterEntityVersion ||
            edit.PosterId != PosterId) return "海报版本或宿主已变化，请重新开始这次调节。";
        try
        {
            var next = PosterPresentationPolicy.Resolve(edit.Command("validate-preview-only").DesiredState);
            if (edit.Setting is not (PosterSetting.Scale or PosterSetting.Spacing or PosterSetting.Content or PosterSetting.Template)) return null;
            var preferences = next.Parameters.Preferences ?? new();
            ContentLayoutCommandPolicy.ValidateAuthoring(ContentDocument, preferences.Scale, preferences.Spacing);
            if (_authoritativePoster?.IsVisible == true && _monitor is not null && _bounds.IsUsable)
                RequireWritableContentProjection(EffectiveContentDocument, ContentViewportFor(_bounds) with
                    { Scale = preferences.Scale, Spacing = preferences.Spacing,
                      HeightDip = Math.Max(0, ContentViewportFor(_bounds).HeightDip + _presentation.MetaHeightDip - next.MetaHeightDip
                          + _presentation.HeaderHeightDip - next.HeaderHeightDip) });
            return null;
        }
        catch (ArgumentException error) { return error.Message; }
    }
    internal bool PreviewCenterPresentation(PosterSettingEdit edit)
    {
        if (ValidateCenterPresentation(edit) is not null || _authoritativePoster?.IsVisible != true) return false;
        var next = PosterPresentationPolicy.Resolve(edit.Command("visual-preview-only").DesiredState);
        _centerPresentation = next;
        _centerPresentationVersion = _posterEntityVersion;
        ApplyPresentation(_centerPresentation);
        // The existing frame and input layout are reconciled at exactly the same bounds.
        // No position/size intention, no activation, no state revision.
        return ApplyFrame(_bounds);
    }
    internal void ClearCenterPresentation()
    {
        if (_centerPresentation is null) return;
        _centerPresentation = null;
        if (!_disposed && _window is not null && _authoritativePoster is not null)
        {
            ApplyPresentation(PosterPresentationPolicy.Resolve(_authoritativePoster));
            ApplyFrame(_bounds);
        }
    }
}
