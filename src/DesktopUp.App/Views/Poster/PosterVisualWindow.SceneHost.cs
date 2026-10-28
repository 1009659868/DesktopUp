using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private IPosterSceneInstance? _sceneInstance;
    private SceneFailureBoundary _sceneFailure = new();
    private PointerScreenPoint? _lastScenePointer;
    private double _lastSceneSeconds;
    private bool _lastSceneCanAnimate;

    private FrameworkElement? CreateScene()
    {
        ReleaseScene();
        _sceneFailure = new();
        FrameworkElement? root = null;
        RunScene(() =>
        {
            _sceneInstance = SceneModuleRegistry.Create(CurrentSceneParameters.Capability);
            if (_sceneInstance is null) return;
            root = _sceneInstance.Root;
            root.IsHitTestVisible = false;
            root.SizeChanged += SceneSizeChanged;
        });
        UpdateScene();
        return _sceneInstance is null ? null : root;
    }

    private void SceneSizeChanged(object sender, SizeChangedEventArgs args) => UpdateScene();
    internal void RefreshScene() => UpdateScene();
    internal void StopSceneMotionFrame() => RunScene(() => _sceneInstance?.StopMotion());

    private void UpdateScene()
    {
        if (_sceneInstance is null) return;
        // A content repaint/press is not a leave event. Reuse only the last shared sample
        // until the next tick (33ms), in screen coordinates so movement cannot shift the aim.
        PaintSceneMotion(_lastSceneSeconds, _lastScenePointer, _lastSceneCanAnimate);
        bool visible = !_disposed && _window is not null && !_geometry.IsCollapsed && !_contentSession.IsSuspended;
        bool requested = false;
        RunScene(() => requested = _sceneInstance?.WantsMotion == true);
        spatial.SetSceneMotionDemand(this, visible && !IsEditing && requested &&
            _sceneFailure.Failure is null, inputRouter);
    }

    internal void PaintSceneMotion(double seconds, PointerScreenPoint? pointer, bool canAnimate)
    {
        if (_sceneInstance is null) return;
        _lastSceneSeconds = seconds; _lastScenePointer = pointer; _lastSceneCanAnimate = canAnimate;
        bool visible = !_disposed && !_geometry.IsCollapsed && !_contentSession.IsSuspended;
        var reasons = (!visible ? ScenePauseReason.Unavailable : ScenePauseReason.None) |
            (IsEditing ? ScenePauseReason.Editing : ScenePauseReason.None) |
            (!canAnimate || spatial.SceneGlobalMotionPaused ? ScenePauseReason.SharedPolicy : ScenePauseReason.None);
        ScenePointer? local = pointer is { } p ? new(
            (p.X - _bounds.X) / (_monitor?.ScaleX ?? 1),
            (p.Y - _bounds.Y) / (_monitor?.ScaleY ?? 1)) : null;
        if (RunScene(() =>
        {
            var root = _sceneInstance!.Root;
            root.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            var frame = SceneFrameContext.Create(root.ActualWidth, root.ActualHeight, seconds, local, visible, reasons) with
            { HeaderBottom = HeaderHeightDip, ContentTop = ContentTopDip, FooterTop = Math.Max(0, root.ActualHeight - FooterHeightDip),
                TitleChrome = _presentation.TitleChrome, TitleMotionPaused = _inputIsland?.CanAnimateTitle != true ||
                    _transitionLayout is not null || _spatialGesture is not null };
            if (_sceneInstance is CanvasSceneInstance && frame.Width > 0 && CanvasFor(_bounds) is { } canvas)
                frame = frame with { Width = canvas.Width, Height = canvas.Height };
            _sceneInstance.Update(CurrentSceneParameters, frame);
            if (_sceneInstance is IPosterTitleMotion motion) TryPaintTitleMotion(motion.TitlePose);
        })) PaintSceneCards();
    }

    private bool RunScene(Action action)
    {
        if (_sceneFailure.Failure is not null) return false;
        if (_sceneFailure.TryRun(action)) return true;
        log.Write("warning", "scene.instance_failed", new { PosterId, _sceneFailure.Failure!.Message, Error = _sceneFailure.Failure.ToString() });
        ReleaseScene();
        PaintSceneCards();
        return false;
    }

    private void ReleaseScene()
    {
        _lastScenePointer = null; _lastSceneSeconds = 0; _lastSceneCanAnimate = false;
        spatial.SetSceneMotionDemand(this, false, inputRouter);
        var instance = _sceneInstance; _sceneInstance = null;
        if (instance is null) return;
        try
        {
            var root = instance.Root;
            root.SizeChanged -= SceneSizeChanged;
            if (VisualTreeHelper.GetParent(root) is Panel parent) parent.Children.Remove(root);
        }
        catch (Exception error) { log.Write("warning", "scene.detach_failed", new { PosterId, error.Message }); }
        try { instance.Dispose(); }
        catch (Exception error) { log.Write("warning", "scene.dispose_failed", new { PosterId, error.Message }); }
    }
}
