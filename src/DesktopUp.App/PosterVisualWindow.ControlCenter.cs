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
    internal bool PreviewCenterPresentation(PosterSettingEdit edit)
    {
        if (_disposed || _window is null || _activeCommandRequestId is not null || edit.Basis.EntityVersion != _posterEntityVersion ||
            edit.PosterId != PosterId || _authoritativePoster?.IsVisible != true) return false;
        var desired = edit.Command("visual-preview-only").DesiredState;
        var next=PosterPresentationPolicy.Resolve(desired);
        var preferences=next.Parameters.Preferences??new();
        try { ContentLayoutCommandPolicy.ValidateAuthoring(ContentDocument,preferences.Scale,preferences.Spacing); }
        catch(ArgumentException) {return false;}
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
