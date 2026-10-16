using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

public partial class App
{
    // Bounded R7-C1 fixture; original command/session/R4/center ownership, no OS input,
    // no resource-budget/soak claim. Guarded by the existing isolated lifetime entry.
    private async void RunContinuousLifetimeProbe()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(175));
        using var lease = _ledger!.Acquire("fixture.continuous-lifetime", "r7-c1-20-rounds");
        const int rounds = 20;
        int completed = 0, count = -1, commands = 0;
        string stage = "ready"; string? error = null; bool passed = false;
        void Stop() => lifetime.Cancel();
        _stopAutoSmoke = Stop;
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException($"Round {completed + 1}, {stage}: {message}"); }
        async Task Until(Func<bool> predicate)
        {
            while (!predicate()) await Task.Delay(20, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
        }
        Task Idle() => Until(() => _autoReady && !_controlCenterEnvironmentPending &&
            !_spatialRuntime!.IsSpatialBusy && _spatialRuntime.PendingPosturesForProbe == 0);
        void Edit(string id, PosterStateCommandKind kind, Func<PosterDefinition, PosterDefinition> edit)
        {
            var before = Current(); var target = before.PostersById[id];
            var result = _stateCommands!.Execute(new PosterStateCommand("c1-edit-" + Guid.NewGuid().ToString("N"),
                StateCommandSource.ControlCenter, kind, before.Revision, id, edit(target.Poster))
                { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) });
            Require(result.Succeeded && result.CurrentRevision == before.Revision + 1,
                result.Error ?? "State edit must commit once.");
            commands++;
        }
        async Task Posture(string id, bool collapsed)
        {
            var before = Current(); var target = before.PostersById[id];
            var done = new TaskCompletionSource<StateCommandResult>();
            _spatialRuntime!.SetCollapsed(new("c1-posture-" + Guid.NewGuid().ToString("N"),
                StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed, before.Revision, id,
                target.Poster with { IsCollapsed = collapsed })
                { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) }, r => done.TrySetResult(r));
            var result = await done.Task.WaitAsync(lifetime.Token);
            Require(result.Succeeded && result.CurrentRevision == before.Revision + 1,
                result.Error ?? "Posture must commit once.");
            commands++; await Idle();
            Require(Current().PostersById[id].Poster.IsCollapsed == collapsed, "Posture lost final state.");
        }
        void Snapshot(string phase)
        {
            var p = Current(); int visible = p.Posters.Count(x => x.Poster.IsVisible);
            var frames = _spatialRuntime!.InspectPresentation(p);
            Require(_posterSessions!.Sessions.Count == visible && _inputRouter.ReadyCount == visible,
                "Session/input ownership mismatch.");
            if (frames.Count != visible || frames.Any(f => !f.XamlVerified))
                _log!.Write("info", "auto_lifetime.presentation_mismatch", new { phase, frames,
                    details = p.Posters.Where(x => x.Poster.IsVisible).Select(x =>
                        _spatialRuntime.BackdropWindowForProbe(x.Poster.PosterId).ReadContinuityPresentationForProbe(x)).ToArray() });
            Require(frames.Count == visible && frames.All(f => f.XamlVerified), "Presentation not restored.");
            Require(_spatialRuntime.PostureGroupsForProbe == 0 && _spatialRuntime.PendingPosturesForProbe == 0,
                "Pending posture work survived idle.");
            foreach (string kind in new[] { "timer.auto-posture", "timer.scene-motion", "window.control-center" })
                Require(_ledger!.ActiveResources.Count(x => x == kind) <= 1, "Duplicate " + kind);
            if (visible == 0)
                Require(_autoTimer is null && !_inputRouter.HasPointerObservers &&
                    !_ledger!.ActiveResources.Contains("timer.scene-motion"), "Hidden catalog kept polling/animation.");
            _log!.Write("info", "auto_lifetime.snapshot", new { phase, p.Revision, mode = "continuous",
                posters = p.Posters.Select(x => x.Poster).ToArray(), sessions = _posterSessions.Sessions,
                frameCount = frames.Count, resources = _ledger!.ActiveResources.ToArray() });
        }
        try
        {
            await Until(() => _spatialRuntime is not null && _autoReady); await Idle();
            var start = Current(); count = start.Posters.Count;
            Require(count is 0 or 1 or 3 or 6, "Requires fresh 0/1/3/6 fixture.");
            Require(start.Posters.All(x => x.Poster.IsVisible && !x.Poster.IsCollapsed && !x.Poster.IsLocked && !x.AutoPosture.Enabled),
                "Use expanded/unlocked/auto-disabled fixtures.");
            var ids = start.Posters.Select(x => x.Poster.PosterId).ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                // Mix both optional scene modules with the existing minimal template.
                if (i % 3 == 2) continue;
                string template = i % 3 == 0 ? "rain" : "lamp";
                var recipe = TemplateRecipeCatalog.BuiltIns.Single(x => x.Id == template);
                Edit(ids[i], PosterStateCommandKind.SetPresentation, p => p with { TemplateKey = template,
                    LayoutParametersJson = PosterPresentationPolicy.Serialize(
                        TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Resolve(p).Parameters, recipe), p.LayoutParametersJson) });
            }
            await Idle();
            var baseline = Current();
            var initialBounds = _spatialRuntime!.InspectPresentation(baseline).ToDictionary(x => x.PosterId, x => x.Bounds);
            long basisRevision = baseline.Revision;
            int basisCommands = commands;
            while (completed < rounds)
            {
                stage = "center-open";
                _controlCenter!.Show();
                await Until(() => _controlCenter.VerifyProjection(Current()));
                Require(_ledger!.ActiveResources.Count(x => x == "window.control-center") == 1, "Missing center lease.");
                stage = "collapse";
                foreach (string id in ids) await Posture(id, true);
                Snapshot("collapsed");
                stage = "hide";
                var epochs = _posterSessions!.Sessions.ToDictionary(x => x.PosterId, x => x.Epoch);
                foreach (string id in ids) Edit(id, PosterStateCommandKind.SetVisible, p => p with { IsVisible = false });
                await Idle(); Snapshot("hidden");
                stage = "center-close";
                _controlCenter.WindowForPortableProbe!.Close();
                await Until(() => _controlCenter.WindowForPortableProbe is null);
                Require(!_ledger.ActiveResources.Contains("window.control-center"), "Closed center retained lease.");
                stage = "show";
                foreach (string id in ids) Edit(id, PosterStateCommandKind.SetVisible, p => p with { IsVisible = true });
                await Idle(); Snapshot("reshown");
                Require(_posterSessions.Sessions.All(s => s.Epoch > epochs[s.PosterId]), "Reused retired session epoch.");
                stage = "expand-without-center";
                foreach (string id in ids.Reverse()) await Posture(id, false);
                var final = Current();
                Require(final.Revision == basisRevision + commands - basisCommands, "Unexpected/missing database writes.");
                Require(final.Posters.Count == count && final.Posters.All(p =>
                {
                    var before = baseline.PostersById[p.Poster.PosterId].Poster;
                    // Fresh fixture anchors are normalized hints (.04/.12), not a capture of
                    // this PC's rounded pixels. Only the FIRST commit may materialize them.
                    // All author fields and actual frame bounds remain exact; later rounds
                    // compare the entire persisted definition including these machine fields.
                    var observed = completed == 0 ? p.Poster with
                    {
                        MonitorDeviceName = before.MonitorDeviceName, FallbackXDip = before.FallbackXDip,
                        FallbackYDip = before.FallbackYDip, HorizontalAnchor = before.HorizontalAnchor,
                        VerticalAnchor = before.VerticalAnchor, NormalizedWidth = before.NormalizedWidth,
                        NormalizedExpandedHeight = before.NormalizedExpandedHeight
                    } : p.Poster;
                    return observed == before;
                }), "Cycle changed author state or drifted after initial pixel materialization.");
                Require(_spatialRuntime.InspectPresentation(final).All(f => f.Bounds == initialBounds[f.PosterId]),
                    "Cycle moved/resized the actual expanded frame.");
                Require(final.ControlCenter.Settings.Settings == baseline.ControlCenter.Settings.Settings &&
                    final.ControlCenter.Settings.EntityVersion == baseline.ControlCenter.Settings.EntityVersion &&
                    final.ControlCenter.Settings.SettingsRevision == baseline.ControlCenter.Settings.SettingsRevision,
                    "Cycle changed global settings.");
                if (completed == 0) baseline = final;
                Snapshot("round-final"); completed++;
                _log!.Write("info", "auto_lifetime.round_completed", new { completed, rounds, count, commands, final.Revision });
            }
            // Leave enabled settings for the next fresh process to verify exact persistence.
            // Automatic/manual overlap is separately exercised by the original RR group probe.
            stage = "enable-for-restart";
            foreach (string id in ids) Edit(id, PosterStateCommandKind.SetAutoPosture,
                p => p with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 300 });
            await Idle(); Snapshot("final"); passed = true;
        }
        catch (Exception e) { error = e.ToString(); }
        finally
        {
            if (_stopAutoSmoke == Stop) _stopAutoSmoke = null;
            _log!.Write(passed ? "info" : "error", "auto_lifetime.smoke_completed", new { passed, error, stage,
                mode = "continuous", count, completed, rounds, commands, nativeInput = false, longRun = false });
        }
        lease.Dispose(); ExitApplication();
    }
}

internal sealed partial class PosterVisualWindow
{
    internal object ReadContinuityPresentationForProbe(PosterSurfaceProjection target) => new
    {
        PosterId, presentationMatches = PresentationReadbackEquality.Same(_presentation, target.Presentation),
        titleMatches = _renderedTitle == target.Poster.Title,
        windowTitleMatches = _window?.Title == "DesktopUp V3 · " + target.Poster.Title,
        backdrop = VerifyWindowBackdrop(),
        metaHeight = (_surface?.Child as Microsoft.UI.Xaml.Controls.Grid)?.RowDefinitions[1].Height.Value,
        expectedMetaHeight = target.Poster.IsCollapsed ? 0 : target.Presentation.MetaHeightDip,
        hasCanvas = _contentCanvas is not null, canvasAttached = _itemsGrid?.Children.Contains(_contentCanvas!) == true,
        snapshotOwner = _contentSnapshot?.PosterId, snapshotVersion = _contentSnapshot?.DocumentVersion,
        documentVersion = ContentDocument.DocumentVersion,
        cards = _contentCards.Select(pair => new { pair.Key, pair.Value.Width, pair.Value.Height,
            x = Microsoft.UI.Xaml.Controls.Canvas.GetLeft(pair.Value), y = Microsoft.UI.Xaml.Controls.Canvas.GetTop(pair.Value),
            slot = _contentSnapshot?.Items.FirstOrDefault(x => x.ItemId == pair.Key)?.Bounds,
            _contentSnapshot?.ScrollXDip, _contentSnapshot?.ScrollYDip }).ToArray()
    };
}
