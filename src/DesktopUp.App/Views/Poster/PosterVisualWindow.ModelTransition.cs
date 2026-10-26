using DesktopUp.Domain;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private EventHandler<object>? _modelRendering;
    private Action? _resetModelOpacity;

    // Target geometry and native input have already committed together. Only opacity changes
    // on a rendering tick; no solving, icon extraction, hit rebuilding or background timer.
    private void StartModelTransition(ContentModelTransition? transition)
    {
        CancelModelTransition();
        if (transition is not { Kind: ContentModelTransitionKind.Fade, DurationMs: > 0 } ||
            !ContentDocument.Activity.AllowTransitions || !spatial.CanAnimateContent(SpatialDispatcher) ||
            _contentSession.ProposalsProtected || Presenter.Surface is not { } surface) return;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        _resetModelOpacity = () => surface.Opacity = 1;
        surface.Opacity = .65;
        EventHandler<object>? handler = null;
        handler = (_, _) =>
        {
            if (!ReferenceEquals(handler, _modelRendering)) return;
            double progress = Math.Clamp(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds / transition.DurationMs, 0, 1);
            if (_disposed || _contentSession.ProposalsProtected || spatial.IsSpatialBusy || spatial.SceneReducedMotion || spatial.SceneGlobalMotionPaused ||
                _authoritativePoster?.IsCollapsed == true || progress >= 1)
            { CancelModelTransition(); return; }
            double eased = transition.Easing == ContentModelEasing.EaseOut ? 1 - (1 - progress) * (1 - progress) : progress;
            surface.Opacity = .65 + .35 * eased;
        };
        _modelRendering = handler;
        CompositionTarget.Rendering += handler;
    }

    private void CancelModelTransition()
    {
        if (_modelRendering is { } handler) CompositionTarget.Rendering -= handler;
        _modelRendering = null;
        _resetModelOpacity?.Invoke();
        _resetModelOpacity = null;
    }
}
