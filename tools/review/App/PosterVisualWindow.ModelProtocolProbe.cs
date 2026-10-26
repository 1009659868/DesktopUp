using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal async Task VerifyModelProtocolForProbe(CancellationToken cancellation)
    {
        void Require(bool value, string message) { if (!value) throw new IOException(message); }
        async Task Until(Func<bool> ready)
        { while (!ready()) await Task.Delay(20, cancellation); }
        var previous = ContentDocument.Activity;
        var recent = previous with { ModelId = ContentModelProtocol.RecentId };
        Require(EditContent(new(ContentLayoutEditKind.Activity) { Activity = recent, Enabled = true }) is null, "Model switch edit failed.");
        await Until(() => ActiveContentHeat?.Model?.Id == ContentModelProtocol.RecentId && !spatial.IsSpatialBusy &&
            ReadContentSurfaceForSmoke() is { CanvasMatches: true, InputMatches: true });
        await Until(() => _modelRendering is null);
        Require(EffectiveContentDocument.Placements.Any(p => p.ModelPosition is not null), "Model positions did not reach the effective layout.");
        Require(ContentDocument.Placements.All(p => p.ModelPosition is null), "Model positions leaked into author state.");
        var native = ReadContentSurfaceForSmoke();
        // LastItemReachable is a current-clip readback, not a theoretical reachability flag.
        // Exercise the production scroll policy before asking whether the tail is visible.
        ScrollItems(-12000);
        Require(ReadContentSurfaceForSmoke() is { LastItemReachable: true, CanvasMatches: true, InputMatches: true },
            "Model strip tail is not reachable by the production scroll path.");
        ScrollContentForSmoke(0, 0);
        native = ReadContentSurfaceForSmoke();
        bool reduced = spatial.SceneReducedMotion;
        await spatial.WithModelAnimationPreferenceForProbe(true, async () =>
        {
            StartModelTransition(new(ContentModelTransitionKind.Fade, 200));
            Require(_modelRendering is not null && Presenter.Surface!.Opacity < 1, "Fade did not subscribe.");
            await Until(() => _modelRendering is null);
            Require(Presenter.Surface!.Opacity == 1 && ReadContentSurfaceForSmoke().GeometryFingerprint == native.GeometryFingerprint,
                "Fade changed geometry or leaked its rendering subscription.");
            StartModelTransition(new(ContentModelTransitionKind.Fade, 300));
            _contentSession.SetInteraction(PosterContentInteraction.Menu, true);
            try { await Until(() => _modelRendering is null); }
            finally { _contentSession.SetInteraction(PosterContentInteraction.Menu, false); CancelModelTransition(); }
            Require(Presenter.Surface!.Opacity == 1, "Interruption left content translucent.");
        });
        await spatial.WithModelAnimationPreferenceForProbe(false, () =>
        {
            StartModelTransition(new(ContentModelTransitionKind.Fade, 200));
            Require(_modelRendering is null && Presenter.Surface!.Opacity == 1, "Reduced motion did not skip transition.");
            return Task.CompletedTask;
        });
        Require(EditContent(new(ContentLayoutEditKind.Activity) { Activity = previous, Enabled = true }) is null, "Return to builtin failed.");
        await Until(() => ActiveContentHeat?.Model?.Id == previous.ModelId && !spatial.IsSpatialBusy &&
            ReadContentSurfaceForSmoke() is { CanvasMatches: true, InputMatches: true });
        Require(EffectiveContentDocument.Placements.All(p => p.ModelPosition is null) && _modelRendering is null,
            "Switching back retained old model motion/animation.");
        log.Write("info", "contract.model_protocol_passed", new { runtimeSwitch = true, positionApplied = true,
            sameGenerationInput = true, renderingReleased = true, interruptionReleased = true,
            originalReducedMotion = reduced, simulatedPreferenceBranches = true, nativeMouse = false, measuredDisplayFps = false });
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal async Task WithModelAnimationPreferenceForProbe(bool enabled, Func<Task> action)
    {
        bool previous = _sceneAnimationsEnabled;
        try { _sceneAnimationsEnabled = enabled; await action(); }
        finally { _sceneAnimationsEnabled = previous; }
    }
}
