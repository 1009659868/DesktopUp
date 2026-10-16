using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Explicit isolated profile only: invokes the real command/query/registry/XAML path.
    // The test removes fixture poster definitions, never desktop content or files.
    private void RunMultiInstanceSmoke(bool restartOnly)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.IsRepeating = true;
        var deadline = DateTime.UtcNow.AddSeconds(19);
        int step = 0;
        string a = "", b = "", c = "";
        long oldBEpoch = 0;
        Dictionary<string, PosterDefinition>? expected = null;
        Dictionary<string, PosterSpatialRuntime.PresentationReadback>? initialFrames = null;
        PosterSurfaceProjection? staleB = null, staleC = null;
        PosterRemovalPreview? staleRemoval = null;
        timer.Tick += Tick; timer.Start();

        void Finish(bool passed, string? error)
        {
            timer.Stop(); timer.Tick -= Tick;
            _log!.Write(passed ? "info" : "error", "multi_instance.smoke_completed", new { passed, error, step, restartOnly });
        }
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Success(StateCommandResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException($"{result.Status}: {result.Error}");
        }
        void Set(string id, PosterStateCommandKind kind, Func<PosterDefinition, PosterDefinition> change)
        {
            var current = Current(); var target = current.PostersById[id]; var desired = change(target.Poster);
            Success(_stateCommands!.Execute(new PosterStateCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                kind, current.Revision, id, desired) { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) }));
            expected![id] = desired;
        }
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        void Snapshot(string phase)
        {
            var current = Current();
            var visible = current.Posters.Where(p => p.Poster.IsVisible).Select(p => p.Poster.PosterId).ToHashSet();
            var sessions = _posterSessions!.Sessions;
            Require(sessions.Count == visible.Count && sessions.All(s => s.Status == PosterSessionStatus.Ready && visible.Contains(s.PosterId)), "session-set-not-current");
            Require(_inputRouter.ReadyCount == visible.Count && _inputRouter.Capture is null, "input-set-or-capture-invalid");
            Require(_controlCenter!.VerifyProjection(current) && _tray!.VerifyProjection(current.Tray), "three-surface-projection-mismatch");
            var resources = _ledger!.ActiveResources;
            Require(resources.Count(r => r == "window.control-center") == 1 && resources.Count(r => r == "tray.desktopup") == 1, "duplicate-or-missing-global-surface");
            Require(current.Posters.Count == expected!.Count && current.Posters.All(p => expected.GetValueOrDefault(p.Poster.PosterId) == p.Poster), "unexpected-persisted-poster-change");
            var frames = _spatialRuntime!.InspectPresentation(current);
            Require(frames.Count == visible.Count && frames.Select(f => f.WindowHandle).Distinct().Count() == visible.Count &&
                frames.All(f => f.XamlVerified && f.EntityVersion == current.PostersById[f.PosterId].EntityVersion), "native-or-xaml-readback-invalid");
            if (initialFrames is not null)
                foreach (var frame in frames)
                {
                    Require(frame.Bounds == initialFrames[frame.PosterId].Bounds, "parameter-or-visibility-changed-bounds");
                    if (frame.PosterId != b) Require(frame.WindowHandle == initialFrames[frame.PosterId].WindowHandle, "healthy-peer-hwnd-recreated");
                }
            else initialFrames = frames.ToDictionary(f => f.PosterId);
            _log!.Write("info", "multi_instance.snapshot", new { phase, current.Revision,
                posters = current.Posters.Select(p => p.Poster).ToArray(), frames, sessions,
                readyInputs = _inputRouter.ReadyCount, activeResources = resources.ToArray(),
                singleControlCenter = true, singleTray = true, threeSurfaceRevisionVerified = true });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                if (Volatile.Read(ref _exiting) != 0) { timer.Stop(); timer.Tick -= Tick; return; }
                if (DateTime.UtcNow > deadline) { Finish(false, "timeout"); return; }
                if (_controlCenterEnvironmentPending || _posterSessions is null || _spatialRuntime is null) return;
                var current = Current();
                switch (step)
                {
                    case 0:
                        Require(current.Posters.Count == (restartOnly ? 2 : 3), "wrong-fixture-count");
                        if (_posterSessions.Sessions.Any(s => s.Status != PosterSessionStatus.Ready) ||
                            _inputRouter.ReadyCount != current.Posters.Count(p => p.Poster.IsVisible)) return;
                        expected = current.Posters.ToDictionary(p => p.Poster.PosterId, p => p.Poster);
                        _controlCenter!.Show(); _controlCenter.Show();
                        if (restartOnly)
                        {
                            Require(current.Posters.Count(p => p.Poster.IsVisible) == 1, "hidden-poster-resurrected");
                            Snapshot("restart"); Finish(true, null); return;
                        }
                        Require(current.Posters.Select(p => p.Poster.LayoutParametersJson).Distinct().Count() == 3, "fixture-parameters-not-distinct");
                        a = current.Posters[0].Poster.PosterId; b = current.Posters[1].Poster.PosterId; c = current.Posters[2].Poster.PosterId;
                        oldBEpoch = _posterSessions.Sessions.Single(s => s.PosterId == b).Epoch;
                        Snapshot("initial"); break;
                    case 1:
                        var draft = PosterPresentationDraft.Open(current.PostersById[a]);
                        draft = draft with { Title = "A / Games edited", Parameters = draft.Parameters with { Content = draft.Parameters.Content with { IconSizeDip = 40, GapDip = 14 } } };
                        var command = draft.ToCommand(Guid.NewGuid().ToString("N"));
                        Success(_stateCommands!.Execute(command)); expected![a] = command.DesiredState;
                        break;
                    case 2:
                        Snapshot("edited-a"); staleB = current.PostersById[b];
                        Set(b, PosterStateCommandKind.SetVisible, p => p with { IsVisible = false }); break;
                    case 3:
                        Snapshot("hidden-b");
                        var staleCommand = (PosterPresentationDraft.Open(staleB!) with { Title = "must-not-apply" }).ToCommand(Guid.NewGuid().ToString("N"));
                        Require(_stateCommands!.Execute(staleCommand).Status == StateCommandStatus.Conflict, "stale-hidden-command-accepted");
                        Set(b, PosterStateCommandKind.Rename, p => p with { Title = "B / Documents hidden edit" }); break;
                    case 4:
                        Snapshot("edited-hidden-b"); Set(b, PosterStateCommandKind.SetVisible, p => p with { IsVisible = true }); break;
                    case 5:
                        Snapshot("reshown-b");
                        Require(_posterSessions.Sessions.Single(s => s.PosterId == b).Epoch > oldBEpoch, "show-reused-cancelled-incarnation");
                        staleC = current.PostersById[c];
                        var confirmation = new PosterRemovalConfirmation();
                        var ticket = confirmation.Begin(new(c, staleC.EntityVersion, StateCommandSource.ControlCenter), _stateCoordinator!.PrepareRemoval(c)!)!;
                        Require(confirmation.Complete(ticket.Id, false) is null && Current().Revision == current.Revision, "cancelled-removal-wrote-state");
                        staleRemoval = _stateCoordinator.PrepareRemoval(c);
                        Set(c, PosterStateCommandKind.SetLocked, p => p with { IsLocked = !p.IsLocked }); break;
                    case 6:
                        Snapshot("stale-removal-ready");
                        Require(_stateCommands!.Execute(new RemovePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                            staleRemoval!.Revision, staleRemoval.ReadSet, c, true)).Status == StateCommandStatus.Conflict, "stale-removal-accepted");
                        var preview = _stateCoordinator!.PrepareRemoval(c)!;
                        Success(_stateCommands.Execute(new RemovePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                            preview.Revision, preview.ReadSet, c, true)));
                        expected!.Remove(c); break;
                    case 7:
                        Snapshot("deleted-c");
                        Require(_stateCommands!.Execute((PosterPresentationDraft.Open(staleC!) with { Title = "late-callback" }).ToCommand(Guid.NewGuid().ToString("N"))).Status == StateCommandStatus.NotFound, "deleted-poster-resurrected");
                        Set(b, PosterStateCommandKind.SetVisible, p => p with { IsVisible = false }); break;
                    case 8:
                        Snapshot("final-catalog"); _controlCenter!.CloseForContractProbe(); break;
                    case 9:
                        Require(!_ledger!.ActiveResources.Contains("window.control-center"), "control-center-close-leaked-window");
                        Require(_inputRouter.ReadyCount == 1 && _posterSessions.Sessions.Count == 1, "center-close-stopped-posters");
                        _controlCenter!.Show(); _controlCenter.Show(); break;
                    case 10:
                        Snapshot("final"); Finish(true, null); return;
                }
                step++;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
    }
}
