using System.Globalization;
using DesktopUp.Application;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private void RunControlCenterSmoke(string root)
    {
        UnhandledException += (_, args) => _log!.Write("error", "control_center.smoke_unhandled", new
        { args.Message, exception = args.Exception.ToString(), sliders = _controlCenter?.SettingsWindowForContractProbe?.PresetDiagnosticsForProbe() });
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.control-center", "isolated-xaml-r4-check");
        long deadline = _autoClock.ElapsedMilliseconds + 55000, revision = 0;
        int step = 0, initialCount = 0, opens = 0; string a = ""; double height = 0; Task? capture = null;
        bool ticking = false;
        long captureAfter = 0;
        void Stop() { timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Finish(bool passed, string? error)
        { Stop(); _log!.Write(passed ? "info" : "error", "control_center.smoke_completed", new { passed, error, step, initialCount, opens, revision, nativeMouse = false }); ExitApplication(); }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            // Native Invoke/COM may pump messages. Never run the same fixture step
            // recursively or exit while its outer transaction is still on the stack.
            if (ticking) return;
            ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "control center timeout");
                if (!_autoReady) return;
                if (step == 41)
                {
                    Require(_spatialRuntime!.IsSpatialBusy, "conflict fixture missed in-flight geometry");
                    var p = _stateCoordinator!.Refresh(); var t = p.PostersById[a];
                    var changed = _stateCommands!.Execute(new PosterStateCommand("center-smoke-inflight", StateCommandSource.ControlCenter,
                        PosterStateCommandKind.Rename, p.Revision, a, t.Poster with { Title = t.Poster.Title + " / conflict" })
                        { ReadSet = StateReadSet.ForPoster(a, t.EntityVersion) });
                    Require(changed.Succeeded, changed.Error ?? "conflict injection failed"); step = 42;
                }
                if (_spatialRuntime!.IsSpatialBusy) return;
                var current = _stateCoordinator!.Refresh();
                var window = _controlCenter!.SettingsWindowForContractProbe;
                var editor = window?.EditorForProbe;
                switch (step)
                {
                    case 0:
                        initialCount = current.Posters.Count; revision = current.Revision;
                        Require(initialCount is 0 or 1 or 3, "isolated 0/1/3 required");
                        Require(current.Posters.All(p => !p.AutoPosture.Enabled), "auto-OFF required; do not hide deferred M08 bug");
                        _controlCenter.Show(); opens++; step++; break;
                    case 1:
                        if (window?.Content.XamlRoot is null) return;
                        Require(_controlCenter.VerifyProjection(current), "initial projection mismatch");
                        Require(current.Posters.All(p => _spatialRuntime.ReadControlCenterGeometry(p.Poster.PosterId) is not null), "missing actual occupancy");
                        if (initialCount == 0) { window.InvokeEditorForProbe("create"); step = 30; break; }
                        a = current.Posters[0].Poster.PosterId; window.SelectSettingsTargetForProbe(a);
                        window.EditFieldForProbe(false, "X", "-"); step++; break;
                    case 2:
                        Require(editor!.IsDirty && !editor.CanApply && _controlCenter.IsSettingsInteractionActive, "invalid geometry lost/protection missing");
                        _controlCenter.ApplyProjection(current.ControlCenter);
                        Require(editor.Geometry!.X == "-", "refresh overwrote raw draft");
                        window!.InvokeEditorForProbe("geometry-cancel"); step++; break;
                    case 3:
                        Require(!editor!.IsDirty && current.Revision == revision, "cancel mutated geometry");
                        window!.EditFieldForProbe(true, "Icon", "bad"); step++; break;
                    case 4:
                        Require(!editor!.CanApply && editor.IsDirty && _controlCenter.IsSettingsInteractionActive, "invalid style not protected");
                        window!.InvokeEditorForProbe("style-cancel"); step++; break;
                    case 5:
                        if (initialCount > 1)
                        {
                            window!.EditFieldForProbe(true, "Gap", "16");
                            window.SelectSettingsTargetForProbe(current.Posters[1].Poster.PosterId);
                            Require(!editor!.IsDirty, "selection leaked A draft"); window.SelectSettingsTargetForProbe(a);
                        }
                        window!.EditFieldForProbe(true, "Gap", "16"); step++; break;
                    case 6: window!.InvokeEditorForProbe("style"); step++; break;
                    case 7:
                        Require(!editor!.IsDirty && !editor.IsPending && current.Revision == revision + 1 &&
                            current.PostersById[a].Presentation.Parameters.Content.GapDip == 16, "style not committed exactly once");
                        revision = current.Revision;
                        var target = current.PostersById[a]; var effective = _spatialRuntime.GetEffectiveLayout(target)!;
                        Require(effective is not null, "missing effective geometry");
                        height = Math.Max(240, effective!.HeightDip - 20);
                        window!.EditFieldForProbe(false, "X", ((effective.Bounds.X - effective.Environment.WorkArea.X) * 96d / effective.Environment.DpiX).ToString("R", CultureInfo.InvariantCulture));
                        window.EditFieldForProbe(false, "Y", ((effective.Bounds.Y - effective.Environment.WorkArea.Y) * 96d / effective.Environment.DpiY).ToString("R", CultureInfo.InvariantCulture));
                        window.EditFieldForProbe(false, "Height", height.ToString("R", CultureInfo.InvariantCulture)); step++; break;
                    case 8: window!.InvokeEditorForProbe("geometry"); step++; break;
                    case 9:
                        Require(!editor!.IsPending && !editor.IsDirty && editor.Error is null, "R4 geometry apply failed: " + editor!.Error);
                        Require(current.Revision == revision + 1 && Math.Abs(current.PostersById[a].Poster.ExpandedHeightDip-height) < 1, "geometry did not commit once");
                        Require(current.PostersById[a].Presentation.Parameters.Content.GapDip == 16, "geometry changed content metrics");
                        revision = current.Revision; window!.EditFieldForProbe(false, "Height", (height + 20).ToString("R", CultureInfo.InvariantCulture));
                        step = 40; break;
                    case 40: window!.InvokeEditorForProbe("geometry"); step = 41; break;
                    case 42:
                        Require(!editor!.IsPending && editor.IsDirty && editor.Error is not null, "in-flight conflict not reported/preserved");
                        Require(current.Revision == revision + 1 && Math.Abs(current.PostersById[a].Poster.ExpandedHeightDip-height) < 1,
                            "in-flight conflict wrote geometry");
                        Require(Math.Abs(_spatialRuntime.GetEffectiveLayout(current.PostersById[a])!.HeightDip - height) < 1, "committed geometry not restored");
                        revision = current.Revision; window!.InvokeEditorForProbe("geometry-cancel");
                        window.InvokeEditorForProbe("clone"); step = 10; break;
                    case 10:
                        if (!window!.CatalogDialogForProbe) return;
                        window.CancelCatalogForProbe(); step++; break;
                    case 11:
                        if (window!.CatalogDialogForProbe) return;
                        Require(current.Revision == revision, "cancel clone created entity");
                        window.EditFieldForProbe(true, "Gap", "18"); _controlCenter.CloseForContractProbe(); step++; break;
                    case 12:
                        Require(window is null && !_controlCenter.IsSettingsInteractionActive && current.Revision == revision, "close leaked draft/protection");
                        _controlCenter.Show(); opens++; step++; break;
                    case 13:
                        if (window?.Content.XamlRoot is null) return;
                        Require(!editor!.IsDirty && _controlCenter.VerifyProjection(current), "reopen projection mismatch");
                        if (opens < 4) { _controlCenter.CloseForContractProbe(); step = 12; break; }
                        window.RevealEditorForProbe(); captureAfter = _autoClock.ElapsedMilliseconds + 1000; step++; break;
                    case 14:
                        if (_autoClock.ElapsedMilliseconds < captureAfter) return;
                        capture = window!.CaptureSettingsForProbeAsync(Path.Combine(root, "control-center-editor.png")); step++; break;
                    case 15:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult(); Require(current.Revision == revision, "unexpected late mutation");
                        Finish(true, null); break;
                    case 30:
                        if (!window!.CatalogDialogForProbe) return;
                        window.CancelCatalogForProbe(); step++; break;
                    case 31:
                        if (window!.CatalogDialogForProbe) return;
                        Require(current.Posters.Count == 0 && current.Revision == revision && !editor!.CanApply, "empty cancel changed catalog");
                        var created = _spatialRuntime.ApplyControlCenterCatalog(new("center-smoke-create", current.Revision,
                            new StateReadSet(new Dictionary<string,long>(), CatalogVersion: current.CatalogVersion), null, "Created in empty catalog"));
                        Require(created.Succeeded, created.Error ?? "empty create failed"); revision = created.CurrentRevision; step++; break;
                    case 32:
                        Require(current.Posters.Count == 1 && current.Revision == revision && _controlCenter.VerifyProjection(current), "created entity not projected");
                        _controlCenter.CloseForContractProbe(); step = 12; break;
                }
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
