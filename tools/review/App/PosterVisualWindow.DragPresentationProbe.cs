using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Isolated, real HWND/XAML measurement. No SendInput, database commit or file movement.
    internal async Task VerifyDragPresentationForProbe(CancellationToken cancellation)
    {
        var original = _bounds;
        var revision = _stateRevision;
        var observations = new List<object>();
        void Require(bool ok, string message)
        { if (!ok) throw new IOException("drag presentation: " + message); }
        Require(!IsEditing && _transitionLayout is null && _spatialGesture is null, "Idle fixture required.");
        RefreshContentSurface();
        var identity = await CaptureRenderIdentityForProbe();
        try
        {
            _spatialGesture = spatial.Begin(this, original, PosterGestureKind.Move, PosterOperatingMode.Using, 1, 1);
            Require(_spatialGesture is not null, "Could not begin isolated move.");
            var destination = new[] { (2, 0), (-2, 0), (0, 2), (0, -2) }
                .Select(d => original with { X = original.X + d.Item1, Y = original.Y + d.Item2 })
                .FirstOrDefault(b => _spatialGesture!.Preview(PosterSpatialRuntime.Rect(b)).Changes
                    .Any(c => c.After == PosterSpatialRuntime.Rect(b)));
            Require(destination.IsUsable, "No safe two-pixel comparison path.");
            foreach (bool reuse in new[] { false, true })
            {
                var elapsed = new List<double>(); long allocated = 0; int repaints = 0;
                for (int i = 0; i < 32; i++)
                {
                    var frame = i % 2 == 0 ? destination : original;
                    var completion = new TaskCompletionSource();
                    spatial.RequestDragFrame(this, () =>
                    {
                        try
                        {
                            var paint = Presenter.Frame;
                            long bytes = GC.GetAllocatedBytesForCurrentThread();
                            long start = Stopwatch.GetTimestamp();
                            bool ok = reuse ? PresentSpatialCandidate(frame) : ApplyFrame(frame);
                            elapsed.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                            allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
                            Require(ok, "Native publication rejected.");
                            if (!ReferenceEquals(paint, Presenter.Frame)) repaints++;
                            var surface = inputRouter.ReadSurface(inputSession);
                            Require(surface?.Frame == frame &&
                                surface.Layout.ContentSnapshotGeneration == Presenter.Frame!.Snapshot.ViewportGeneration,
                                "Paint, native position and input generation disagree.");
                            RequireRetainedRenderForProbe(identity);
                            completion.SetResult();
                        }
                        catch (Exception error) { completion.SetException(error); }
                    });
                    await completion.Task.WaitAsync(cancellation);
                }
                if (reuse) Require(repaints == 0, "Translation repainted retained content.");
                else Require(repaints == 32, "Comparison did not exercise the full content path.");
                elapsed.Sort();
                observations.Add(new { path = reuse ? "translation" : "full-content-control", samples = elapsed.Count,
                    medianMs = elapsed[elapsed.Count / 2], p95Ms = elapsed[(int)(elapsed.Count * .95)], allocatedBytes = allocated,
                    contentRepaints = repaints });
            }
            Require(_stateRevision == revision, "Preview wrote author state.");
            CancelSpatialGesture("drag-probe-cancel");
            Require(_bounds == original && _spatialGesture is null && _pendingGestureFrame is null,
                "Cancellation failed to restore the accepted frame.");
            VerifyContentPublicationForProbe(); // stale pair / native failure / recovery
            DragProbeNative.ResetRegion(_inputIsland!.WindowHandle);
            var recovery = PrepareSpatialFrame(_bounds)!;
            Require(CommitSpatialFrames([recovery]) && _inputIsland.VerifyTitleRegionForProbe(_bounds.Height),
                "Unchanged-size native input region reset was hidden by reuse.");
            log.Write("info", "contract.drag_presentation_passed", new { observations, nativeMouse = false,
                measuredDisplayFps = false, unchangedRevision = revision, cancellationRestored = true });
        }
        finally
        {
            spatial.CancelDragFrame(this);
            if (_spatialGesture is not null) CancelSpatialGesture("drag-probe-cleanup");
        }
    }
}

internal static class DragProbeNative
{
    internal static void ResetRegion(nint window)
    {
        nint region = CreateRectRgn(0, 0, 1, 1);
        if (region != 0 && SetWindowRgn(window, region, false) != 0) return;
        if (region != 0) DeleteObject(region);
        throw new IOException("Could not inject owned input region reset.");
    }
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint region);
}
