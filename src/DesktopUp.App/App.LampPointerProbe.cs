using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed record LampPointerMoveRequest(string RequestId, string Stage, int ProcessId, string PosterId,
    string ObservedPosterId, long VisualHandle, long InputHandle, DesktopPixelRect Frame, PointerScreenPoint Point,
    string CoordinateSpace = "physical-screen", string Action = "move", bool ButtonsAllowed = false);

public partial class App
{
    // Dedicated isolated-fixture observation: only the external, guarded driver moves the real
    // cursor. This code neither injects input nor calls PaintLampMotion/its synthetic probe API.
    private void RunLampPointerProbe(string dataRoot)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.lamp-pointer", "real-passive-pointer-observation");
        string requestPath = Path.Combine(dataRoot, "lamp-pointer-request.json");
        string responsePath = Path.Combine(dataRoot, "lamp-pointer-response.json");
        var observations = new List<object>();
        long deadline = _autoClock.ElapsedMilliseconds + 90000, nextSnapshot = 0, requestAt = 0, acknowledgedAt = 0;
        long issuedSequence = 0, firstOutsideAt = 0, lastAcceptedSequence = 0;
        string a = "", b = ""; int phase = 0, snapshots = 0, acceptedSamples = 0;
        bool stopped = false, ticking = false;
        LampPointerMoveRequest? request = null;
        LampSceneObservation? outsideStart = null;
        var requests = new Queue<LampPointerMoveRequest>();
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, bool blocked, string? reason)
        {
            var diagnostic = _spatialRuntime?.ReadLampPointerDiagnosticsForProbe();
            LampSceneObservation? scene = a.Length > 0 && _spatialRuntime?.ContentVisualReadyForProbe(a) == true
                ? _spatialRuntime.ReadLampNativeForProbe(a).Scene : null;
            Stop();
            _log!.Write(passed || blocked ? "info" : "error", "lamp.pointer_observed", new
            {
                passed, blocked, reason, phase, stage = request?.Stage, snapshots, observations,
                lastDiagnostic = diagnostic, lastScene = scene,
                pointerDriver = "guarded-external-native-move-only", syntheticPaint = false,
                nativeButtons = false, nativeKeyboard = false, desktopPixelVisualCertified = false,
                uiCProductAccepted = false, milestoneAccepted = false
            });
            ExitApplication();
        }
        void Apply(PosterSettingEdit edit)
        {
            var result = _stateCommands!.Execute(edit.Command("lamp-pointer-setup-" + Guid.NewGuid().ToString("N")));
            Require(result.Succeeded, result.Error ?? "Formal lamp setup failed.");
        }
        static PointerScreenPoint Center(PosterInputSurfaceSnapshot surface, PosterInputRegion region) =>
            new(surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
                surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2);
        LampPointerMoveRequest Request(string stage, PosterPointerTarget target, PosterInputSurfaceSnapshot surface, PointerScreenPoint point) =>
            new(Guid.NewGuid().ToString("N"), stage, Environment.ProcessId, target.Session.PosterId, a,
                target.Visual.ToInt64(), target.Input.ToInt64(), surface.Frame, point);
        void PublishNext()
        {
            if (requests.Count == 0) { Finish(true, false, null); return; }
            request = requests.Dequeue(); requestAt = _autoClock.ElapsedMilliseconds;
            acknowledgedAt = 0; acceptedSamples = 0; outsideStart = null; firstOutsideAt = 0; lastAcceptedSequence = 0;
            issuedSequence = _spatialRuntime!.ReadLampPointerDiagnosticsForProbe()?.Sequence ?? 0;
            string temporary = requestPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, requestPath, overwrite: true);
            _log!.Write("info", "lamp.pointer_move_requested", request);
        }
        (string? Status, string? Reason) ReadResponse()
        {
            if (request is null || !File.Exists(responsePath)) return (null, null);
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(responsePath)); var root = document.RootElement;
                string? Read(string key) => root.EnumerateObject().FirstOrDefault(p =>
                    string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)).Value is { ValueKind: JsonValueKind.String } value
                        ? value.GetString() : null;
                return Read("RequestId") == request.RequestId ? (Read("Status"), Read("Reason")) : (null, null);
            }
            catch (JsonException) { return (null, null); }
            catch (IOException) { return (null, null); }
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (stopped || ticking) return; ticking = true;
            try
            {
                long now = _autoClock.ElapsedMilliseconds;
                if (now >= deadline) { Finish(false, true, "real-pointer-observation-timeout"); return; }
                if (!_autoReady || _spatialRuntime is null || _spatialRuntime.IsSpatialBusy) return;
                var current = _stateCoordinator!.Refresh();
                if (phase == 0)
                {
                    Require(current.Posters.Count == 2 && current.Posters.All(p => p.Poster.IsVisible && !p.Poster.IsCollapsed),
                        "Real pointer probe requires two visible expanded isolated fixture posters.");
                    a = current.Posters[0].Poster.PosterId; b = current.Posters[1].Poster.PosterId;
                    if (!_spatialRuntime.ContentVisualReadyForProbe(a) || !_spatialRuntime.ContentVisualReadyForProbe(b)) return;
                    Apply(new(current.PostersById[a], PosterSetting.Template, Text: "lamp")); phase = 1; return;
                }
                if (!_spatialRuntime.ContentVisualReadyForProbe(a) || !_spatialRuntime.ContentVisualReadyForProbe(b)) return;
                if (phase == 1)
                {
                    Require(current.PostersById[a].Poster.TemplateKey == "lamp", "Formal template selection is not visible.");
                    Apply(new(current.PostersById[a], PosterSetting.TemplateParameters, Lamp: LampTemplatePolicy.Defaults));
                    phase = 2; requestAt = now; return;
                }
                var native = _spatialRuntime.ReadLampNativeForProbe(a);
                var diagnostic = _spatialRuntime.ReadLampPointerDiagnosticsForProbe();
                if (now >= nextSnapshot)
                {
                    nextSnapshot = now + 500; snapshots++;
                    _log!.Write("info", "lamp.pointer_sample", new { phase, stage = request?.Stage, diagnostic,
                        scene = native.Scene, native.Parameters, runtimeTimerPresent = _spatialRuntime.LampMotionRunning });
                }
                if (native.Scene.ReducedMotion || native.Scene.GlobalMotionPaused || !native.Parameters.MotionEnabled || !native.Parameters.FollowPointer)
                { Finish(false, true, "motion-disabled-by-current-settings-or-system-preference"); return; }
                if (phase == 2)
                {
                    // A short real-current-cursor baseline precedes any driver request.
                    if (diagnostic is null || now - requestAt < 1000) return;
                    var targets = _spatialRuntime.ReadPointerTargets();
                    var targetA = targets.Single(t => t.Session.PosterId == a);
                    var targetB = targets.Single(t => t.Session.PosterId == b);
                    var surfaceA = _inputRouter.ReadSurface(targetA.Session)
                        ?? throw new InvalidOperationException("Observed poster input surface is unavailable.");
                    var surfaceB = _inputRouter.ReadSurface(targetB.Session)
                        ?? throw new InvalidOperationException("Peer poster input surface is unavailable.");
                    var inside = surfaceA.Layout.Regions.Where(r => r.Kind == PosterInputTargetKind.ContentItem &&
                        r.Bounds.Width >= 12 && r.Bounds.Height >= 12).Select(r => Center(surfaceA, r)).ToArray();
                    Require(inside.Length >= 2, "Two safe, real content Item centers are required.");
                    var first = inside[0];
                    var second = inside.OrderByDescending(p => Math.Abs(p.X - first.X) + Math.Abs(p.Y - first.Y)).First();
                    Require(Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) >= 30, "Item centers are not separated.");
                    var peer = surfaceB.Layout.Regions.Where(r => r.Kind == PosterInputTargetKind.ContentItem &&
                        r.Bounds.Width >= 12 && r.Bounds.Height >= 12).Select(r => Center(surfaceB, r))
                        .Where(p => !PosterInputGeometry.Contains(surfaceA, p)).ToArray();
                    Require(peer.Length > 0, "A safe peer Item outside the observed poster is required.");
                    requests.Enqueue(Request("inside-1", targetA, surfaceA, first));
                    requests.Enqueue(Request("inside-2", targetA, surfaceA, second));
                    requests.Enqueue(Request("outside-peer", targetB, surfaceB, peer[0]));
                    phase = 3; PublishNext(); return;
                }
                if (request is null) return;
                var response = ReadResponse();
                if (string.Equals(response.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                { Finish(false, true, "driver-blocked: " + response.Reason); return; }
                if (!string.Equals(response.Status, "moved", StringComparison.OrdinalIgnoreCase))
                {
                    if (now - requestAt > 15000) Finish(false, true, "move-only-driver-response-timeout");
                    return;
                }
                if (acknowledgedAt == 0) acknowledgedAt = now;
                if (now - acknowledgedAt > 5000)
                { Finish(false, true, "native-pointer-did-not-establish-the-requested-scene-state"); return; }
                if (diagnostic is null || diagnostic.Sequence <= issuedSequence || diagnostic.Sequence == lastAcceptedSequence ||
                    diagnostic.Position is not { } point || Math.Abs(point.X - request.Point.X) > 2 || Math.Abs(point.Y - request.Point.Y) > 2) return;
                var observed = diagnostic.Posters.SingleOrDefault(p => p.Session.PosterId == a);
                if (!diagnostic.Reliable || observed is null ||
                    (diagnostic.SharedProtection | observed.Protection) != AutoPostureProtection.None) return;
                bool outside = request.Stage == "outside-peer";
                if (observed.Presence != (outside ? AutoPosturePresence.Outside : AutoPosturePresence.Inside)) return;
                // An occluded point must never pass as the deliberate outside case.
                if (outside && observed.Reason != "outside-published-frame") return;
                if (!outside)
                {
                    if (observed.PublishedFrame is not { Width: > 0, Height: > 0 } frame ||
                        native.Scene.Width <= 0 || native.Scene.Height <= 0) return;
                    double x = (point.X - frame.X) * native.Scene.Width / frame.Width;
                    double y = (point.Y - frame.Y) * native.Scene.Height / frame.Height;
                    if (!native.Scene.FollowingPointer || Math.Abs(native.Scene.LightX - x) > 2 || Math.Abs(native.Scene.LightY - y) > 2) return;
                    acceptedSamples++; lastAcceptedSequence = diagnostic.Sequence;
                    if (acceptedSamples < 3) return;
                }
                else
                {
                    if (native.Scene.FollowingPointer || !native.Scene.MotionRunning) return;
                    if (outsideStart is null) { outsideStart = native.Scene; firstOutsideAt = now; return; }
                    if (now - firstOutsideAt < 500 || native.Scene.MotionFrames <= outsideStart.MotionFrames ||
                        Math.Abs(native.Scene.FixtureAngle - outsideStart.FixtureAngle) < .0001 ||
                        Math.Abs(native.Scene.LightX - outsideStart.LightX) < .0001) return;
                }
                observations.Add(new { stage = request.Stage, request, diagnostic, scene = native.Scene, outsideStart,
                    nativeMoveOnly = true, syntheticPaint = false });
                PublishNext();
            }
            catch (Exception error) { Finish(false, false, error.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
