using System.ComponentModel;
using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

public interface IPosterDropRegistration : IDisposable
{
    bool IsReady { get; }
    bool IsHovering { get; }
}

/// <summary>Optional, bounded OLE surface. Never changes the ordinary input island's HWND,
/// style, region, capture or router. The native host owns geometry/attachment/lifetime.</summary>
internal sealed class PosterDropReception : IPosterDropRegistration
{
    private readonly IDesktopLayerAttachment attachment;
    private readonly Func<IncomingDropSurface?> surface;
    private readonly IStructuredLog log;
    private readonly SubclassProc procedure;
    private readonly Func<bool> restoreCluster;
    private NativeFileDropTarget? target;
    private NativeOleDragMonitor? monitor;
    private IDisposable? windowLease, monitorLease;
    private nint window;
    private DesktopPixelRect frame;
    private DesktopShellHostSnapshot host;
    private bool disposed, suspended, oleActive, armed, finished, faulted;
    public bool IsReady => !disposed && !faulted && target is not null;
    public bool IsHovering => target?.IsHovering == true;
    internal nint WindowHandle => window;

    internal PosterDropReception(IDesktopLayerAttachment attachment, DesktopShellHostSnapshot host,
        DesktopPixelRect frame, Func<IncomingDropSurface?> surface, Action<IncomingDropTicket> accept,
        IResourceLedger ledger, IStructuredLog log, Action<IncomingDropPreview?>? changed, Func<bool> restoreCluster)
    {
        this.attachment = attachment; this.host = host; this.frame = frame; this.surface = surface; this.log = log; this.restoreCluster = restoreCluster;
        procedure = WindowProc;
        try
        {
            window = CreateWindowEx(0x08080080, "STATIC", "", 0x80000000, frame.X, frame.Y, frame.Width, frame.Height, 0, 0, 0, 0);
            if (window == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            windowLease = ledger.Acquire("window.poster-drop-reception", "bounded-ole-surface");
            if (!SetWindowSubclass(window, procedure, 1, 0) || !SetLayeredWindowAttributes(window, 0, 1, 2) || !SetRegion(false))
                throw new IOException("Cannot create non-intercepting drop component.");
            if (!attachment.Attach(window, host, showWindow: true, inputTransparent: false).Succeeded)
                throw new IOException("Cannot attach drop component to desktop host.");
            target = new NativeFileDropTarget(window, CurrentSurface, accept, ledger, log, changed);
            target.DropFinished = () => { finished = true; KillTimer(window, 0xD05); SetArmed(false); };
            monitorLease = ledger.Acquire("ole.capture-observation", "shared-out-of-context-subscription");
            monitor = new NativeOleDragMonitor(active =>
            {
                // A previous Drop may arrive after capture-end. Every new capture starts
                // a fresh reception, regardless of that final callback's ordering.
                oleActive = active; finished = false;
                if (active) KillTimer(window, 0xD05);
                Refresh();
                // Windows can release OLE capture before delivering the final Drop callback.
                // Remove the interception immediately, but let the already-admitted protocol
                // finish. A bounded grace only cleans an abandoned session; it never moves files.
                if (!active && target?.IsHovering == true) SetTimer(window, 0xD05, 500, 0);
            });
            log.Write("info", "poster.drop_component_created", new { handle = $"0x{window:X}", frame, ordinaryInput = "unchanged" });
        }
        catch { Dispose(); throw; }
    }
    private IncomingDropSurface? CurrentSurface() => IsReady && !suspended && !finished && (oleActive || target?.IsHovering == true) ? surface() : null;
    private void Refresh()
    {
        if (disposed || faulted) return;
        try { SetArmed(!suspended && oleActive && !finished && surface() is not null); }
        catch (Exception e) { Fault(e.Message); }
    }
    private bool SetRegion(bool full)
    {
        nint region = CreateRectRgn(0, 0, full ? frame.Width : 0, full ? frame.Height : 0);
        if (region == 0) return false;
        if (SetWindowRgn(window, region, false) != 0) return true;
        DeleteObject(region); return false;
    }
    private void SetArmed(bool value)
    {
        if (disposed || faulted || value == armed) return;
        if (!SetRegion(value)) { Fault("drop-region-update-failed"); return; }
        armed = value;
        if (value && !restoreCluster()) Fault("drop-layer-restore-failed");
    }
    private void Fault(string reason)
    {
        if (disposed || faulted) return;
        faulted = true; armed = false;
        // Fail closed independently of the ordinary island. Hide even if GDI mutation failed.
        ShowWindow(window, 0); SetLayeredWindowAttributes(window, 0, 0, 2);
        target?.DragLeave(); log.Write("warning", "poster.drop_component_disabled", new { reason });
    }
    internal void ApplyFrame(DesktopPixelRect next)
    {
        if (disposed || faulted) return;
        if (!next.IsUsable) { Suspend(); return; }
        if (frame != next)
        {
            SetArmed(false); target?.DragLeave(); frame = next;
            if (!SetWindowPos(window, 0, next.X, next.Y, next.Width, next.Height, 0x14)) { Fault("drop-frame-update-failed"); return; }
        }
        suspended = false; Refresh();
    }
    internal void Suspend()
    { if (disposed) return; suspended = true; SetArmed(false); target?.DragLeave(); }
    internal void Reattach(DesktopShellHostSnapshot next)
    {
        if (disposed || faulted) return;
        Suspend(); host = next;
        if (!attachment.Attach(window, next, showWindow: true, inputTransparent: false).Succeeded) Fault("drop-reattach-failed");
    }
    internal void Reassert(DesktopShellHostSnapshot next)
    {
        if (!disposed && !faulted && !attachment.ReassertDesktopBottom(window, next)) Fault("drop-zorder-failed");
    }
    private nint WindowProc(nint hwnd, uint msg, nuint w, nint l, nuint id, nuint data)
    {
        if (msg == 0x21) return 3; // never activate, never synthesize/forward a click
        if (msg == 0x113 && w == 0xD05) { KillTimer(hwnd, 0xD05); if (!oleActive) target?.DragLeave(); return 0; }
        return DefSubclassProc(hwnd, msg, w, l);
    }
    public void Dispose()
    {
        if (disposed) return;
        void Cleanup(Action action) { try { action(); } catch (Exception e) { log.Write("warning", "poster.drop_cleanup_failed", new { e.Message }); } }
        Cleanup(Suspend); disposed = true;
        Cleanup(() => monitor?.Dispose()); monitor = null; Cleanup(() => monitorLease?.Dispose()); monitorLease = null;
        Cleanup(() => target?.Dispose()); target = null;
        if (window != 0)
        {
            KillTimer(window, 0xD05); ShowWindow(window, 0); Cleanup(() => attachment.Detach(window)); RemoveWindowSubclass(window, procedure, 1);
            DestroyWindow(window); window = 0;
        }
        Cleanup(() => windowLease?.Dispose()); windowLease = null; GC.KeepAlive(procedure);
    }
    private delegate nint SubclassProc(nint h, uint m, nuint w, nint l, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint h, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint h, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint h, uint m, nuint w, nint l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint h, nint r, bool redraw);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint o);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint h, int command);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll")] private static extern nuint SetTimer(nint h, nuint id, uint interval, nint proc);
    [DllImport("user32.dll")] private static extern bool KillTimer(nint h, nuint id);
}
