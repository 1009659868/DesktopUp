using System.Runtime.InteropServices;
using System.Text;

namespace DesktopUp.Windows;

/// <summary>
/// UI-thread shared adapter for the Windows OLE capture window. Capture notifications only
/// wake validation: a mouse button, arbitrary capture, or accessibility drag event is not enough.
/// CLIPBRDWNDCLASS is a Windows implementation detail, not an OLE API promise. Unknown hosts
/// fail closed (ordinary desktop input is preserved). File authority still requires IDropTarget.
/// </summary>
public sealed class NativeOleDragMonitor : IDisposable
{
    [ThreadStatic] private static Shared? current;
    private Shared? owner;
    private readonly Action<bool> changed;
    public bool IsDragging => owner?.Active == true;
    public NativeOleDragMonitor(Action<bool> changed)
    {
        this.changed = changed;
        owner = current ??= new Shared(); owner.Listeners.Add(changed);
        try { changed(owner.Active); } catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        var value = owner; owner = null; if (value is null) return;
        value.Listeners.Remove(changed);
        if (value.Listeners.Count == 0) { value.Dispose(); if (ReferenceEquals(current, value)) current = null; }
    }
    private sealed class Shared : IDisposable
    {
        internal readonly List<Action<bool>> Listeners = [];
        internal bool Active;
        private readonly WinEvent callback;
        private readonly TimerProc tick;
        private nint hook, capture;
        private uint thread;
        private nuint timer;
        private bool disposed;
        internal Shared()
        {
            callback = OnCapture; tick = (_, _, _, _) => Validate();
            hook = SetWinEventHook(8, 9, 0, callback, 0, 0, 0); // OUTOFCONTEXT, no injection.
            if (hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }
        private void OnCapture(nint h, uint ev, nint hwnd, int obj, int child, uint sourceThread, uint time)
        {
            if (disposed) return;
            try
            {
                if (ev == 8 && IsOleCapture(hwnd, sourceThread))
                { capture = hwnd; thread = sourceThread; SetActive(true); }
                else if (Active && (sourceThread == thread || hwnd == capture)) Validate();
            }
            catch { SetActive(false); }
        }
        private void Validate()
        { try { if (!IsOleCapture(capture, thread)) SetActive(false); } catch { SetActive(false); } }
        private void SetActive(bool value)
        {
            if (disposed || value == Active) return;
            Active = value;
            if (value)
            {
                timer = SetTimer(0, 0, 30, tick);
                if (timer == 0) { Active = false; capture = 0; thread = 0; return; }
            }
            else
            {
                if (timer != 0) { KillTimer(0, timer); timer = 0; }
                capture = 0; thread = 0;
            }
            foreach (var listener in Listeners.ToArray()) try { listener(Active); } catch { /* consumer has its own failure boundary */ }
        }
        public void Dispose()
        { disposed = true; if (timer != 0) KillTimer(0, timer); if (hook != 0) UnhookWinEvent(hook); timer = 0; hook = 0; Active = false; GC.KeepAlive(callback); GC.KeepAlive(tick); }
    }
    private static bool IsOleCapture(nint window, uint thread)
    {
        if (window == 0 || thread == 0 || GetWindowThreadProcessId(window, out _) != thread) return false;
        var text = new StringBuilder(128);
        if (GetClassName(window, text, text.Capacity) == 0 || text.ToString() != "CLIPBRDWNDCLASS") return false;
        var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
        if (!GetGUIThreadInfo(thread, ref info) || info.Capture != window || info.MenuOwner != 0 || info.MoveSize != 0) return false;
        nint desktop = OpenInputDesktop(0, false, 1); if (desktop == 0) return false;
        try { return DesktopName(desktop) is { } name && name == DesktopName(GetThreadDesktop(GetCurrentThreadId())); }
        finally { CloseDesktop(desktop); }
    }
    private static string? DesktopName(nint desktop)
    { var text = new StringBuilder(256); return desktop != 0 && GetUserObjectInformation(desktop, 2, text, text.Capacity * 2, out _) ? text.ToString() : null; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo { public uint Size, Flags; public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret; public Rect CaretRect; }
    private delegate void WinEvent(nint hook, uint ev, nint hwnd, int obj, int child, uint thread, uint time);
    private delegate void TimerProc(nint hwnd, uint message, nuint id, uint time);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEvent proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nuint SetTimer(nint hwnd, nuint id, uint interval, TimerProc callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(nint hwnd, nuint id);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")] private static extern int GetClassName(nint hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] private static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetUserObjectInformationW")] private static extern bool GetUserObjectInformation(nint obj, int index, StringBuilder text, int size, out int needed);
}
