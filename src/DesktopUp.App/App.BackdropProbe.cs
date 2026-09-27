using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinRT;

namespace DesktopUp;

internal sealed record BackdropPostureProbeFrame(string RequestId, string PosterId, bool Collapsed,
    int AcceptedFrame, int TotalFrames, DesktopPixelRect InitialBounds, DesktopPixelRect FinalBounds,
    DesktopPixelRect VisibleBounds);

internal sealed record BackdropMotionObservation(string Name, string File, double Progress,
    BackdropPostureProbeFrame Frame, DesktopPixelRect NativeRegionBefore, DesktopPixelRect NativeRegionAfter,
    LampNativeReadback Before, LampNativeReadback After);

internal sealed partial class PosterSpatialRuntime
{
    internal PosterVisualWindow BackdropWindowForProbe(string id) => _windows[id];
    // Null in production. The isolated probe can pace three frames without changing the
    // animation schedule, cursor, native commit, or watchdog's elapsed-time budget.
    internal Func<BackdropPostureProbeFrame, Task>? BackdropFrameObserverForProbe { get; set; }
}

public partial class App
{
    // Explicit isolated-profile entry only. Capture this app's own HWND, including its native
    // composition backdrop; RenderTargetBitmap deliberately excludes that layer.
    private async void RunBackdropProbe(string dataRoot)
    {
        using var lease = _ledger!.Acquire("fixture.backdrop", "isolated-native-backdrop-capture");
        using var cancellation = new CancellationTokenSource();
        var rows = new List<object>();
        var motionFrames = new List<BackdropMotionObservation>();
        bool motion = _startupIntent.Arguments.Contains("--backdrop-motion-capture", StringComparer.Ordinal);
        bool secondPoster = _startupIntent.Arguments.Contains("--backdrop-second-poster", StringComparer.Ordinal);
        string id = "poster-main";
        Task? activeCapture = null;
        void Stop()
        {
            if (_spatialRuntime is not null) _spatialRuntime.BackdropFrameObserverForProbe = null;
            cancellation.Cancel();
            lease.Dispose();
        }
        _stopAutoSmoke = Stop;
        bool passed = false; string? error = null;
        try
        {
            if (secondPoster)
                id = _stateCoordinator!.Refresh().Posters.Select(p => p.Poster.PosterId)
                    .Where(p => p != "poster-main").Single();
            for (int i = 0; !_autoReady || _spatialRuntime!.IsSpatialBusy ||
                !_spatialRuntime.ContentVisualReadyForProbe(id); i++)
            {
                if (i >= 100) throw new TimeoutException("Poster did not become ready.");
                await Task.Delay(100, cancellation.Token);
            }
            var window = _spatialRuntime.BackdropWindowForProbe(id);
            DesktopPixelRect VisibleBounds() => _inputRouter.ReadSurface(window.ReadPointerTarget().Session)?.Frame
                ?? throw new InvalidOperationException("Poster input surface is unavailable.");
            async Task Capture(string name)
            {
                await Task.Delay(700, cancellation.Token);
                string file = Path.Combine(dataRoot, "backdrop-" + name + ".png");
                activeCapture = BackdropCaptureProbe.CaptureAsync(window.WindowHandle, file, cancellation: cancellation.Token);
                await activeCapture;
                rows.Add(new { name, file, visibleBounds = VisibleBounds(),
                    nativeRegion = BackdropCaptureProbe.ReadRegion(window.WindowHandle), readback = window.ReadLampNativeForProbe() });
            }
            async Task Posture(bool collapsed)
            {
                var current = _stateCoordinator!.Refresh(); var target = current.PostersById[id];
                var completion = new TaskCompletionSource<StateCommandResult>();
                _spatialRuntime.SetCollapsed(new("backdrop-probe-" + Guid.NewGuid().ToString("N"),
                    StateCommandSource.ControlCenter, PosterStateCommandKind.SetCollapsed,
                    current.Revision, id, target.Poster with { IsCollapsed = collapsed })
                    { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) }, result => completion.TrySetResult(result));
                var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(12), cancellation.Token);
                if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            }
            await Capture("initial");
            if (motion)
            {
                await Posture(true); await Capture("collapsed-1");
                double[] thresholds = [.2, .5, .8];
                string[] stages = ["early", "middle", "late"];
                async Task CaptureMotion(BackdropPostureProbeFrame frame, double progress, string name)
                {
                    // Yield to native presentation while AdvancePosture keeps this exact HRGN.
                    // This is phase-paced native capture, not original-speed frame certification.
                    await Task.Delay(60, cancellation.Token);
                    if (VisibleBounds() != frame.VisibleBounds)
                        throw new InvalidOperationException("Visible frame changed before phase capture.");
                    var region = BackdropCaptureProbe.ReadRegion(window.WindowHandle);
                    var expectedRegion = new DesktopPixelRect(0, 0, frame.VisibleBounds.Width, frame.VisibleBounds.Height);
                    if (region != expectedRegion) throw new InvalidOperationException("Native HRGN does not match the sampled phase.");
                    var before = window.ReadLampNativeForProbe();
                    string file = Path.Combine(dataRoot, "backdrop-motion-" + name + ".png");
                    await BackdropCaptureProbe.CaptureAsync(window.WindowHandle, file, TimeSpan.FromSeconds(2), cancellation.Token);
                    var after = window.ReadLampNativeForProbe();
                    var afterRegion = BackdropCaptureProbe.ReadRegion(window.WindowHandle);
                    if (VisibleBounds() != frame.VisibleBounds || afterRegion != region)
                        throw new InvalidOperationException("Visible HRGN changed during phase capture.");
                    motionFrames.Add(new(name, file, progress, frame, region, afterRegion, before, after));
                    if (!before.BackdropValid || !after.BackdropValid || before.BackdropColor != after.BackdropColor)
                        throw new InvalidOperationException("Backdrop color or attachment changed during phase capture.");
                }
                _spatialRuntime.BackdropFrameObserverForProbe = frame =>
                {
                    if (frame.PosterId != id || frame.Collapsed || motionFrames.Count >= thresholds.Length)
                        return Task.CompletedTask;
                    double span = frame.FinalBounds.Height - frame.InitialBounds.Height;
                    double progress = span <= 0 ? 0 : (frame.VisibleBounds.Height - frame.InitialBounds.Height) / span;
                    if (progress < thresholds[motionFrames.Count] || progress >= 1)
                        return Task.CompletedTask;
                    if (motionFrames.Any(p => p.Frame.VisibleBounds.Height == frame.VisibleBounds.Height))
                        throw new InvalidOperationException("Phase capture did not advance visible height.");
                    activeCapture = CaptureMotion(frame, progress, stages[motionFrames.Count]);
                    return activeCapture;
                };
                await Posture(false);
                _spatialRuntime.BackdropFrameObserverForProbe = null;
                if (motionFrames.Count != 3) throw new InvalidOperationException("Expansion did not expose three intermediate HRGN phases.");
                await Capture("expanded-1");
            }
            else
            {
                for (int i = 1; i <= 2; i++)
                {
                    await Posture(true); await Capture("collapsed-" + i);
                    await Posture(false); await Capture("expanded-" + i);
                }
            }
            // One variable at a time: same HWND/geometry/tint, first the existing redraw flags,
            // then explicitly request background erasure. Neither is a production workaround.
            BackdropCaptureProbe.Redraw(window.WindowHandle, erase: false);
            await Capture("ordinary-redraw");
            BackdropCaptureProbe.Redraw(window.WindowHandle, erase: true);
            await Capture("erase-redraw");
            passed = true;
        }
        catch (Exception failure) { error = failure.ToString(); }
        finally
        {
            if (_spatialRuntime is not null) _spatialRuntime.BackdropFrameObserverForProbe = null;
            cancellation.Cancel();
            // A watchdog cancellation can finish posture before its capture. Consume that task
            // before releasing the fixture or requesting normal exit, including failure paths.
            if (activeCapture is not null)
                try { await activeCapture; }
                catch (Exception failure) { passed = false; error ??= failure.ToString(); }
            if (_stopAutoSmoke == Stop) _stopAutoSmoke = null;
        }
        var report = new { passed, error, rows, motionFrames, posterId = id, secondPoster,
            phasePaced = motion, originalSpeedCertified = false, animationFramesCertified = false,
            nativeMouse = false, ownWindowCapture = true,
            visualAcceptance = false, note = "Inspect native captures; API readback alone is not visual PASS." };
        await File.WriteAllTextAsync(Path.Combine(dataRoot, "backdrop-observation.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _log!.Write(passed ? "info" : "error", "backdrop.probe_observed", new { passed, error, captures = rows.Count });
        lease.Dispose();
        ExitApplication();
    }
}

// Bounded diagnostic lifetime; no production timer, desktop capture, input injection or
// global setting changes. Native references are released even on capture timeout.
internal static class BackdropCaptureProbe
{
    public static void Redraw(nint window, bool erase)
    {
        if (!RedrawWindow(window, 0, 0, 0x0001u | 0x0080u | 0x0100u | (erase ? 0x0004u : 0)))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public static DesktopPixelRect ReadRegion(nint window)
    {
        nint region = CreateRectRgn(0, 0, 0, 0);
        if (region == 0) throw new InvalidOperationException("Could not allocate native region readback.");
        try
        {
            if (GetWindowRgn(window, region) == 0 || GetRgnBox(region, out var bounds) == 0)
                throw new InvalidOperationException("Native region readback failed.");
            return new(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        }
        finally { DeleteObject(region); }
    }

    public static async Task CaptureAsync(nint window, string path, TimeSpan? frameTimeout = null,
        CancellationToken cancellation = default)
    {
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Window capture is unavailable.");
        nint d3d = 0, context = 0, dxgi = 0, inspectable = 0;
        try
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out d3d, out _, out context));
            var dxgiId = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3d, in dxgiId, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            using var device = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
            var item = CreateItem(window);
            using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
            using var session = pool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            var completion = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            int capturing = 0;
            bool frameClaimed = false;
            void Arrived(Direct3D11CaptureFramePool sender, object args)
            {
                if (Interlocked.CompareExchange(ref capturing, 1, 0) != 0) return;
                try
                {
                    var frame = sender.TryGetNextFrame();
                    if (frame is null) { Interlocked.Exchange(ref capturing, 0); return; }
                    if (!completion.TrySetResult(frame)) frame.Dispose();
                }
                catch (Exception error) { completion.TrySetException(error); }
            }
            pool.FrameArrived += Arrived;
            try
            {
                session.StartCapture();
                // Cleanup must not depend on a still-running UI dispatcher during exit.
                using var frame = await completion.Task.WaitAsync(frameTimeout ?? TimeSpan.FromSeconds(8), cancellation).ConfigureAwait(false);
                frameClaimed = true;
                using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
                var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
            }
            finally
            {
                pool.FrameArrived -= Arrived;
                // A timeout can race with the producer publishing a frame. Terminalize the
                // completion first, then dispose any successful but unclaimed handoff.
                completion.TrySetCanceled();
                if (!frameClaimed && completion.Task.IsCompletedSuccessfully) completion.Task.Result.Dispose();
            }
        }
        finally
        {
            if (inspectable != 0) Marshal.Release(inspectable);
            if (dxgi != 0) Marshal.Release(dxgi);
            if (context != 0) Marshal.Release(context);
            if (d3d != 0) Marshal.Release(d3d);
        }
    }
    private static GraphicsCaptureItem CreateItem(nint window)
    {
        string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        nint text = 0, factory = 0, item = 0;
        object? interop = null;
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(name, (uint)name.Length, out text));
            var factoryId = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(text, in factoryId, out factory));
            interop = Marshal.GetObjectForIUnknown(factory);
            var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            item = ((ICaptureItemInterop)interop).CreateForWindow(window, in itemId);
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
        }
        finally
        {
            if (item != 0) Marshal.Release(item);
            if (interop is not null) Marshal.ReleaseComObject(interop);
            if (factory != 0) Marshal.Release(factory);
            if (text != 0) WindowsDeleteString(text);
        }
    }
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICaptureItemInterop
    {
        nint CreateForWindow(nint window, in Guid iid);
        nint CreateForMonitor(nint monitor, in Guid iid);
    }
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string source, uint length, out nint text);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint text);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint text, in Guid iid, out nint factory);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, uint driver,
        nint software, uint flags, nint levels, uint count, uint sdk, out nint device, out uint level, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint device, out nint result);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(nint window, nint rect, nint region, uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint window, nint region);
    [DllImport("gdi32.dll")] private static extern int GetRgnBox(nint region, out NativeRect bounds);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
}
