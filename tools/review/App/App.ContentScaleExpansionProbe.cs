using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

public partial class App
{
    // Runs only after the existing owned cross-poster profile guard; no synthesized OS input.
    private async Task VerifyScaleExpansionForProbe(PosterVisualWindow window, CancellationToken cancellation)
    {
        string id = BuiltInStateIdentities.PrimaryPosterId;
        var runtime = _spatialRuntime!;
        void Require(bool condition, string message) { if (!condition) throw new IOException(message); }
        async Task Until(Func<bool> condition)
        {
            while (!condition()) await Task.Delay(30, cancellation);
        }
        _controlCenter!.Show();
        await Until(() => _controlCenter.SettingsWindowForContractProbe?.Content.XamlRoot is not null);
        var center = _controlCenter.SettingsWindowForContractProbe!;
        center.SelectSettingsTargetForProbe(id);
        center.PreviewScaleForProbe(.4); center.FinishSettingsForProbe();
        await Until(() => !center.CommonSettingsForProbe.ProtectsInteraction);
        var current = _stateCoordinator!.Refresh(); var target = current.PostersById[id];
        Require(target.Presentation.Parameters.Preferences!.Scale == .4, "Initial scale was not committed.");
        Require(ContentFrameMinimumPolicy.TryForPoster(target.Poster, out var min, out _), "No minimum.");
        var basis = runtime.ReadControlCenterGeometry(id)!;
        var request = new ControlCenterGeometryRequest("scale-small-frame", target, current.Revision,
            new(current.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: current.CatalogVersion),
            new(0, 0, Math.Ceiling(min!.WidthDip), target.Poster.ExpandedHeightDip))
            { ActualBasis = basis, ChangedFields = PosterGeometryFields.Width };
        var done = new TaskCompletionSource<StateCommandResult>();
        runtime.ApplyControlCenterGeometry(request, result => done.SetResult(result));
        var resized = await done.Task.WaitAsync(cancellation);
        Require(resized.Succeeded, "Cannot shrink: " + resized.Error);
        await Until(() => !runtime.IsSpatialBusy);
        var small = _stateCoordinator.Refresh();
        int smallPixels = runtime.ReadControlCenterGeometry(id)!.Bounds.Width;
        center.PreviewScaleForProbe(1.2);
        await Until(() => runtime.ReadControlCenterGeometry(id)!.Bounds.Width > smallPixels);
        Require(_stateCoordinator.Refresh().Revision == small.Revision, "A preview wrote state.");
        center.CancelScaleForProbe();
        Require(runtime.ReadControlCenterGeometry(id)!.Bounds.Width == smallPixels &&
            !center.HasPosterDraft(id), "Cancel failed to restore frame or release draft.");
        center.PreviewScaleForProbe(1.2);
        await Until(() => runtime.ReadControlCenterGeometry(id)!.Bounds.Width > smallPixels);
        center.FinishSettingsForProbe();
        await Until(() => !center.CommonSettingsForProbe.ProtectsInteraction);
        current = _stateCoordinator.Refresh(); target = current.PostersById[id];
        Require(current.Revision == small.Revision + 1 && target.Presentation.Parameters.Preferences!.Scale == 1.2 &&
            target.Poster.WidthDip > small.PostersById[id].Poster.WidthDip, "Scale and frame were not one commit.");
        var surface = runtime.ReadContentSurfaceForSmoke(id);
        Require(surface.CanvasMatches && surface.InputMatches, "Expanded content/input diverged.");
        var locked = _stateCommands!.Execute(new PosterStateCommand("scale-lock", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, current.Revision, id, target.Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) });
        Require(locked.Succeeded, "Cannot lock fixture.");
        var blocked = _stateCoordinator.Refresh();
        center.PreviewScaleForProbe(2.8); center.FinishSettingsForProbe();
        Require(!center.HasPosterDraft(id) && center.ScaleValueForProbe == 1.2 &&
            _stateCoordinator.Refresh().Revision == blocked.Revision, "Rejected scale did not spring back/release.");
        target = blocked.PostersById[id];
        var unlocked = _stateCommands.Execute(new PosterStateCommand("scale-unlock", StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, blocked.Revision, id, target.Poster with { IsLocked = false })
            { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) });
        Require(unlocked.Succeeded && !runtime.HasCenterDraft(id) && window.SetEditing(true),
            "Manual frame entry remained blocked after rejection.");
        Require(window.SetEditing(false), "Cannot finish manual frame adjustment.");
        _log!.Write("info", "contract.scale_expansion_passed", new
            { previewExpanded = true, cancelled = true, atomicCommit = true, rejectedDraftReleased = true,
              manualFrameAvailable = true, nativeMouse = false });
    }
}

public sealed partial class ControlCenterWindow
{
    internal void CancelScaleForProbe() => CancelSettingsPreview();
    internal double ScaleValueForProbe => ScaleSlider.Value;
}
