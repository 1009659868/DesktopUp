using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinRT;

namespace DesktopUp;

// Diagnostic-only own-HWND recorder. Uses the existing optional native-probe capability.
// It never awaits inside the production frame observer or changes the animation clock.
internal static partial class BackdropCaptureProbe
{
    // A separate observation plane: no WGC session or capture border. The bounded worker
    // samples only this poster's native rectangle; it never holds the animation observer.
    internal static Task CaptureScreenSequenceAsync(nint window, string directory, Task finished,
        CancellationToken cancellation) => Task.Run(async () =>
    {
        const long budget = 160L * 1024 * 1024;
        const int limit = 240;
        if (Directory.Exists(directory)) throw new IOException("Preserve the previous capture directory.");
        Directory.CreateDirectory(directory);
        var frames = new List<(SoftwareBitmap Bitmap, long Qpc)>();
        long bytes = 0; int screenOccluded = 0, overBudget = 0;
        var deadline = Stopwatch.StartNew();
        try
        {
            while (!finished.IsCompleted)
            {
                cancellation.ThrowIfCancellationRequested();
                if (deadline.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Screen sequence timed out.");
                if (!GetWindowRect(window, out var rect)) throw new InvalidOperationException("Missing own window.");
                long estimate = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top) * 4;
                if (frames.Count >= limit || estimate <= 0 || bytes + estimate > budget) { overBudget++; break; }
                long qpc = Stopwatch.GetTimestamp();
                var bitmap = CaptureOwnScreenRect(window, rect.Right - rect.Left, rect.Bottom - rect.Top);
                if (bitmap is null) screenOccluded++;
                else { bytes += (long)bitmap.PixelWidth * bitmap.PixelHeight * 4; frames.Add((bitmap, qpc)); }
                // BitBlt may already wait for a desktop frame. Do not add another whole
                // refresh interval after it, which would undersample a one-frame defect.
                double remainingMs = 16 - Stopwatch.GetElapsedTime(qpc).TotalMilliseconds;
                if (remainingMs >= 2) await Task.Delay(TimeSpan.FromMilliseconds(remainingMs), cancellation);
                else await Task.Yield();
            }
            await finished.WaitAsync(TimeSpan.FromSeconds(20), cancellation);
            var folder = await StorageFolder.GetFolderFromPathAsync(directory);
            var metadata = new List<object>();
            for (int i = 0; i < frames.Count; i++)
            {
                var value = frames[i]; string name = $"screen-{i:D4}.png";
                var file = await folder.CreateFileAsync(name, CreationCollisionOption.FailIfExists);
                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(value.Bitmap); await encoder.FlushAsync();
                metadata.Add(new { file = name, value.Qpc, width = value.Bitmap.PixelWidth, height = value.Bitmap.PixelHeight });
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "frames.json"), JsonSerializer.Serialize(new
            {
                ownWindowOnly = false, screenOnly = true, screenCropOwnBounds = true, phasePaced = false,
                captureStartsAfterFirstCommit = true, noAnimationPause = true, allPresentedFramesCertified = false,
                frequency = Stopwatch.Frequency, bytes, budget, limit, skipped = 0, overBudget, screenOccluded,
                error = (string?)null, frames = metadata
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (frames.Count < 2) throw new InvalidOperationException("Screen sequence did not produce enough frames.");
        }
        finally { foreach (var value in frames) value.Bitmap.Dispose(); }
    }, cancellation);

    internal static async Task CaptureSequenceAsync(nint window, string directory, Task finished,
        CancellationToken cancellation, bool screenCrop = false, bool startsAfterFirstCommit = true)
    {
        const long budget = 160L * 1024 * 1024;
        const int limit = 240;
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Window capture is unavailable.");
        if (Directory.Exists(directory)) throw new IOException("Preserve the previous capture directory.");
        Directory.CreateDirectory(directory);
        var frames = new List<(SoftwareBitmap Bitmap, long Qpc, double? SystemMs, int Width, int Height, SoftwareBitmap? Screen)>();
        long bytes = 0;
        int closing = 0, skipped = 0, overBudget = 0, screenOccluded = 0;
        Exception? captureError = null;
        var mutex = new SemaphoreSlim(1, 1);
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
            async void Arrived(Direct3D11CaptureFramePool sender, object args)
            {
                if (Volatile.Read(ref closing) != 0) return;
                if (!mutex.Wait(0)) { Interlocked.Increment(ref skipped); return; }
                try
                {
                    if (Volatile.Read(ref closing) != 0) return;
                    using var frame = sender.TryGetNextFrame();
                    if (frame is null) return;
                    long qpc = Stopwatch.GetTimestamp();
                    long size = (long)item.Size.Width * item.Size.Height * 4 * (screenCrop ? 2 : 1);
                    if (frames.Count >= limit || bytes + size > budget)
                    { Interlocked.Increment(ref overBudget); return; }
                    var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                    long actual = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
                    if (bytes + actual > budget) { bitmap.Dispose(); Interlocked.Increment(ref overBudget); return; }
                    SoftwareBitmap? screen = null;
                    try
                    {
                        if (screenCrop)
                        {
                            screen = CaptureOwnScreenRect(window, bitmap.PixelWidth, bitmap.PixelHeight);
                            if (screen is null) Interlocked.Increment(ref screenOccluded);
                        }
                    }
                    catch { bitmap.Dispose(); throw; }
                    if (screen is not null) actual += (long)screen.PixelWidth * screen.PixelHeight * 4;
                    if (bytes + actual > budget) { bitmap.Dispose(); screen?.Dispose(); Interlocked.Increment(ref overBudget); return; }
                    bytes += actual;
                    frames.Add((bitmap, qpc, frame.SystemRelativeTime.TotalMilliseconds,
                        frame.ContentSize.Width, frame.ContentSize.Height, screen));
                }
                catch (Exception error) { captureError ??= error; }
                finally { mutex.Release(); }
            }
            pool.FrameArrived += Arrived;
            try
            {
                session.StartCapture();
                await finished.WaitAsync(TimeSpan.FromSeconds(20), cancellation);
            }
            finally
            {
                Interlocked.Exchange(ref closing, 1);
                pool.FrameArrived -= Arrived;
                // Drain an in-flight copy before disposing GPU objects or writing PNGs.
                await mutex.WaitAsync(); mutex.Release();
            }
            var folder = await StorageFolder.GetFolderFromPathAsync(directory);
            var metadata = new List<object>();
            for (int i = 0; i < frames.Count; i++)
            {
                var value = frames[i];
                string name = $"frame-{i:D4}.png";
                var file = await folder.CreateFileAsync(name, CreationCollisionOption.FailIfExists);
                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(value.Bitmap); await encoder.FlushAsync();
                if (value.Screen is not null)
                {
                    var screenFile = await folder.CreateFileAsync($"screen-{i:D4}.png", CreationCollisionOption.FailIfExists);
                    using var screenStream = await screenFile.OpenAsync(FileAccessMode.ReadWrite);
                    var screenEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, screenStream);
                    screenEncoder.SetSoftwareBitmap(value.Screen); await screenEncoder.FlushAsync();
                }
                metadata.Add(new { file = name, value.Qpc, value.SystemMs, contentWidth = value.Width,
                    contentHeight = value.Height, width = value.Bitmap.PixelWidth, height = value.Bitmap.PixelHeight });
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "frames.json"), JsonSerializer.Serialize(new
            {
                ownWindowOnly = !screenCrop, screenCropOwnBounds = screenCrop, phasePaced = false, captureStartsAfterFirstCommit = startsAfterFirstCommit,
                noAnimationPause = true, allPresentedFramesCertified = false,
                frequency = Stopwatch.Frequency, bytes, budget, limit, skipped, overBudget, screenOccluded,
                error = captureError?.ToString(), frames = metadata
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (captureError is not null) throw new InvalidOperationException("Native sequence capture failed.", captureError);
            if (frames.Count < 2) throw new InvalidOperationException("Native sequence did not produce enough frames.");
        }
        finally
        {
            foreach (var value in frames) { value.Bitmap.Dispose(); value.Screen?.Dispose(); }
            // A late event may still reach the closing check; do not dispose its tiny guard.
            if (inspectable != 0) Marshal.Release(inspectable);
            if (dxgi != 0) Marshal.Release(dxgi);
            if (context != 0) Marshal.Release(context);
            if (d3d != 0) Marshal.Release(d3d);
        }
    }

    // Optional second evidence plane: only the current poster rectangle, never full desktop.
    // It may contain occluders; it neither changes z-order nor treats that image as HWND-only.
    private static SoftwareBitmap? CaptureOwnScreenRect(nint window, int width, int height)
    {
        if (!GetWindowRect(window, out var r) || width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new InvalidOperationException("Unsafe own-window screen crop bounds.");
        width = Math.Min(width, r.Right - r.Left); height = Math.Min(height, r.Bottom - r.Top);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Own-window crop became empty.");
        // Conservative privacy/validity guard. Foreground overlap is not the only possible
        // occlusion (e.g. non-active topmost windows), so remaining crops still need inspection.
        nint foreground = GetForegroundWindow();
        var name = new System.Text.StringBuilder(128); _ = GetClassName(foreground, name, name.Capacity);
        if (foreground != 0 && foreground != window && name.ToString() is not ("Progman" or "WorkerW") &&
            GetWindowRect(foreground, out var front) && front.Left < r.Right && front.Right > r.Left &&
            front.Top < r.Bottom && front.Bottom > r.Top) return null;
        nint dc = GetDC(0), memory = 0, bitmap = 0, previous = 0;
        try
        {
            memory = CreateCompatibleDC(dc);
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(dc, ref info, 0, out var pixels, 0, 0);
            if (dc == 0 || memory == 0 || bitmap == 0 || pixels == 0) throw new InvalidOperationException("Screen crop allocation failed.");
            previous = SelectObject(memory, bitmap);
            if (!BitBlt(memory, 0, 0, width, height, dc, r.Left, r.Top, 0x40CC0020))
                throw new InvalidOperationException("Own-window screen crop failed.");
            var bytes = new byte[checked(width * height * 4)]; Marshal.Copy(pixels, bytes, 0, bytes.Length);
            return SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore);
        }
        finally
        {
            if (previous != 0) SelectObject(memory, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memory != 0) DeleteDC(memory);
            if (dc != 0) ReleaseDC(0, dc);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPelsPerMeter, YPelsPerMeter; public uint ClrUsed, ClrImportant;
    }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint rop);
}
