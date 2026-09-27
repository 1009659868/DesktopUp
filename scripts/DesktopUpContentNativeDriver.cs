using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Loaded by Windows PowerShell Add-Type. This deliberately uses .NET Framework-compatible syntax.
// Sends real OS input only after proving the requested process, input island and input desktop.
public static class DesktopUpContentNativeDriver
{
    public sealed class Result
    {
        public string Status { get; set; }
        public string Action { get; set; }
        public bool Executed { get; set; }
        public bool CompletedAction { get; set; }
        public string FailureReason { get; set; }
        public int ProcessId { get; set; }
        public long InputWindowHandle { get; set; }
        public string ExpectedExecutablePath { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int EndX { get; set; }
        public int EndY { get; set; }
        public uint SystemDoubleClickMilliseconds { get; set; }
        public string InputDesktop { get; set; }
        public string StartHitDiagnostic { get; set; }
        public int MouseMoves { get; set; }
        public int LeftDowns { get; set; }
        public int LeftUps { get; set; }
        public int RightDowns { get; set; }
        public int RightUps { get; set; }
        public int EscapeDowns { get; set; }
        public int EscapeUps { get; set; }
        public int CompletedClicks { get; set; }
        public int CompletedRightClicks { get; set; }
        public bool OwnedCaptureObserved { get; set; }
        public int KeyboardOwnershipChecks { get; set; }
        public long KeyboardForegroundWindowHandle { get; set; }
        public long KeyboardFocusWindowHandle { get; set; }
        public bool CleanupSucceeded { get; set; }
        public string CleanupFailure { get; set; }
        public long DurationMilliseconds { get; set; }
        public long DragMilliseconds { get; set; }
        public string StartedUtc { get; set; }
        public string FinishedUtc { get; set; }
        public string[] Steps { get; set; }
        public string Scope { get; set; }
    }

    public static Result Run(int processId, IntPtr inputWindow, string expectedExecutablePath,
        string action, int startX, int startY, int endX, int endY)
    {
        return new Context(processId, inputWindow, expectedExecutablePath, action,
            new Point(startX, startY), new Point(endX, endY)).Run();
    }

    private sealed class Abort : Exception
    {
        public readonly string Status;
        public Abort(string status, string message) : base(message) { Status = status; }
    }

    private sealed class Context
    {
        private readonly int _pid;
        private readonly IntPtr _window;
        private readonly string _path;
        private readonly string _action;
        private readonly Point _start, _end;
        private readonly Result _result;
        private readonly List<string> _steps = new List<string>();
        private readonly Stopwatch _elapsed = new Stopwatch();
        private Stopwatch _drag;
        private long _firstClickDownAt = -1;
        private bool _leftOwned, _rightOwned, _escapeOwned;
        private uint _windowThread;

        public Context(int pid, IntPtr window, string path, string action, Point start, Point end)
        {
            _pid = pid; _window = window; _path = path; _action = action; _start = start; _end = end;
            _result = new Result { Status = "NotExecuted", Action = action, ProcessId = pid,
                InputWindowHandle = window.ToInt64(), ExpectedExecutablePath = path,
                StartX = start.X, StartY = start.Y, EndX = end.X, EndY = end.Y,
                CleanupSucceeded = true, Scope = "Win32 input delivery only; not UI, pixel or product acceptance." };
        }

        public Result Run()
        {
            IntPtr previousDpi = IntPtr.Zero;
            _result.StartedUtc = DateTimeOffset.UtcNow.ToString("O");
            _elapsed.Start();
            try
            {
                if (_pid <= 0 || _window == IntPtr.Zero || String.IsNullOrWhiteSpace(_path) ||
                    !Path.IsPathRooted(_path) || !File.Exists(_path) ||
                    !String.Equals(Path.GetExtension(_path), ".exe", StringComparison.OrdinalIgnoreCase))
                    throw new Abort("NotExecuted", "Invalid process, window or absolute executable path.");
                if (_action != "move" && _action != "click" && _action != "double-click" && _action != "right-click" &&
                    _action != "drag" && _action != "cancel-drag")
                    throw new Abort("NotExecuted", "Unsupported action.");
                previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
                if (previousDpi == IntPtr.Zero) throw new Abort("NotExecuted", "Cannot establish physical-coordinate DPI context.");
                RequireEnvironment();
                RequireVirtualPoint(_start); RequireVirtualPoint(_end);
                if (Down(0x01)) throw new Abort("NotExecuted", "The left mouse button was already pressed.");
                if (Down(0x02)) throw new Abort("NotExecuted", "The right mouse button was already pressed.");
                if (CaptureWindow(_windowThread) != IntPtr.Zero || CaptureWindow(0) != IntPtr.Zero)
                    throw new Abort("NotExecuted", "Another capture already owns input; the driver did not start.");
                if (_action == "cancel-drag" && (Down(0x1B) || ModifierDown()))
                    throw new Abort("NotExecuted", "Escape or a keyboard modifier was already pressed.");
                // Mouse capture does not route keyboard input. A NOACTIVATE island can own the
                // pointer while another application owns the keyboard, so prove both before any
                // input at all. Never activate a window merely to make this diagnostic runnable.
                if (_action == "cancel-drag") RequireKeyboardDestination();
                IntPtr startReceiver = WindowFromPoint(_start);
                _result.StartHitDiagnostic = DescribeStartHit(startReceiver);
                if (!OwnWindow(startReceiver)) throw new Abort("Occluded", "The starting point is not on the specified input window.");
                _steps.Add("preflight: process/path/window/default-input-desktop/cursor/start-hit verified");

                Move(_start, false);
                RequireActualPointer(_start);
                if (_action == "click" || _action == "double-click")
                {
                    Click(false);
                    if (_action == "double-click")
                    {
                        Thread.Sleep((int)Math.Max(1, Math.Min(40, _result.SystemDoubleClickMilliseconds / 4)));
                        RequireEnvironment(); RequireDestination(_start, false);
                        Click(true);
                    }
                }
                else if (_action == "right-click") RightClick();
                else if (_action != "move") Drag();
                else _steps.Add("move-only: no button, keyboard, capture or activation input sent");
                _result.Status = "Completed";
                _result.CompletedAction = true;
            }
            catch (Abort failure)
            {
                _result.Status = _result.Executed ? "Failed" : failure.Status;
                _result.FailureReason = failure.Message;
            }
            catch (Exception failure)
            {
                _result.Status = _result.Executed ? "Failed" : "NotExecuted";
                _result.FailureReason = failure.GetType().Name + ": " + failure.Message;
            }
            finally
            {
                // Cleanup never sends a new press. Only inputs this invocation successfully pressed
                // can be released, even after the target loses capture or exits unexpectedly.
                if (_escapeOwned) CleanupRelease(true);
                if (_leftOwned) CleanupRelease(false);
                if (_rightOwned) CleanupRelease(false, true);
                if (_drag != null) _drag.Stop();
                if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi);
                if (!_result.CleanupSucceeded) { _result.Status = "Failed"; _result.CompletedAction = false; }
                _result.DurationMilliseconds = _elapsed.ElapsedMilliseconds;
                _result.DragMilliseconds = _drag == null ? 0 : _drag.ElapsedMilliseconds;
                _result.FinishedUtc = DateTimeOffset.UtcNow.ToString("O");
                _result.Steps = _steps.ToArray();
            }
            return _result;
        }

        private void Click(bool second)
        {
            RequireEnvironment(); RequireDestination(_start, false); RequireActualPointer(_start);
            if (second && _elapsed.ElapsedMilliseconds - _firstClickDownAt >= _result.SystemDoubleClickMilliseconds)
                throw new Abort("Failed", "System double-click interval elapsed before the second press; it was not sent.");
            if (!second) _firstClickDownAt = _elapsed.ElapsedMilliseconds;
            MouseButton(true);
            Thread.Sleep(15);
            RequireEnvironment(); RequireDestination(_start, false);
            RequireActualPointer(_start);
            MouseButton(false);
            _result.CompletedClicks++;
            _steps.Add("click: left down/up delivered");
        }

        private void RightClick()
        {
            RequireEnvironment(); RequireDestination(_start, false); RequireActualPointer(_start);
            if (Down(0x01) || Down(0x02))
                throw new Abort("Failed", "A mouse button became pressed before the right click; no right press was sent.");
            RequireNoRightClickCapture();
            RightButton(true);
            Thread.Sleep(15);
            RequireEnvironment(); RequireNoRightClickCapture();
            RequireDestination(_start, false); RequireActualPointer(_start);
            RightButton(false);
            _result.CompletedRightClicks++;
            _steps.Add("right-click: right down/up delivered without capture or keyboard input");
        }

        private void RequireNoRightClickCapture()
        {
            if (CaptureWindow(_windowThread) != IntPtr.Zero || CaptureWindow(0) != IntPtr.Zero)
                throw new Abort("Failed", "Capture changed during the right click; no further context action was sent.");
        }

        private void Drag()
        {
            RequireEnvironment(); RequireDestination(_start, false); RequireActualPointer(_start);
            _drag = Stopwatch.StartNew();
            MouseButton(true);
            for (int attempt = 0; attempt < 6 && !OwnCapture(); attempt++)
            {
                RequireEnvironment(); Thread.Sleep(10);
            }
            if (!OwnCapture()) throw new Abort("Failed", "The specified input window did not acquire capture after the driver press.");
            _result.OwnedCaptureObserved = true;
            for (int step = 1; step <= 5; step++)
            {
                if (_drag.ElapsedMilliseconds >= 350) throw new Abort("Failed", "The bounded drag deadline elapsed; remaining motion was not sent.");
                var point = new Point((int)Math.Round(_start.X + ((double)_end.X - _start.X) * step / 5),
                    (int)Math.Round(_start.Y + ((double)_end.Y - _start.Y) * step / 5));
                Move(point, true);
                Thread.Sleep(25);
                RequireActualPointer(point);
            }
            RequireEnvironment(); RequireDestination(_end, true);
            if (_drag.ElapsedMilliseconds >= 400) throw new Abort("Failed", "Pointer motion exceeded the 400 ms budget.");
            if (_action == "cancel-drag")
            {
                if (Down(0x1B) || ModifierDown()) throw new Abort("Failed", "Escape/modifier state changed; no cancel key was sent.");
                if (!OwnCapture()) throw new Abort("Failed", "Capture ended before cancel; no unowned Escape was sent.");
                RequireEnvironment();
                RequireKeyboardDestination();
                if (!OwnCapture()) throw new Abort("Failed", "Capture changed during keyboard checks; no cancel key was sent.");
                Escape(true);
                // The existing input island observes Escape every 33 ms while it owns capture.
                Thread.Sleep(45);
                Escape(false);
                _steps.Add("cancel: Escape down/up delivered while own capture was witnessed");
            }
            else { RequireDestination(_end, true); RequireActualPointer(_end); }
            MouseButton(false);
            _drag.Stop();
            if (_drag.ElapsedMilliseconds > 400) throw new Abort("Failed", "The completed drag exceeded its 400 ms duration budget.");
            _steps.Add("drag: bounded moves and release delivered");
        }

        private void RequireEnvironment()
        {
            uint owner;
            if (!IsWindow(_window) || (_windowThread = GetWindowThreadProcessId(_window, out owner)) == 0 || owner != (uint)_pid)
                throw new Abort("NotExecuted", "The input window no longer belongs to the expected process.");
            using (var process = Process.GetProcessById(_pid))
            {
                string actual = process.MainModule.FileName;
                if (process.HasExited || !String.Equals(Path.GetFullPath(actual), Path.GetFullPath(_path), StringComparison.OrdinalIgnoreCase))
                    throw new Abort("NotExecuted", "The process executable path does not match the exact expected executable.");
            }
            IntPtr desktop = OpenInputDesktop(0, false, 1);
            if (desktop == IntPtr.Zero) throw new Abort("NotExecuted", "The input desktop cannot be read.");
            try
            {
                string inputName = ObjectName(desktop);
                string threadName = ObjectName(GetThreadDesktop(GetCurrentThreadId()));
                string targetName = ObjectName(GetThreadDesktop(_windowThread));
                if (!String.Equals(inputName, "Default", StringComparison.OrdinalIgnoreCase) || inputName != threadName || inputName != targetName)
                    throw new Abort("NotExecuted", "Driver and target must share the normal Default input desktop.");
                _result.InputDesktop = inputName;
            }
            finally { CloseDesktop(desktop); }
            Point current;
            if (!GetPhysicalCursorPos(out current)) throw new Abort("NotExecuted", "The physical cursor cannot be read.");
            _result.SystemDoubleClickMilliseconds = GetDoubleClickTime();
            if (_result.SystemDoubleClickMilliseconds == 0) throw new Abort("NotExecuted", "The system double-click policy is unavailable.");
        }

        private bool OwnWindow(IntPtr candidate)
        {
            if (candidate == IntPtr.Zero) return false;
            uint owner;
            GetWindowThreadProcessId(candidate, out owner);
            return owner == (uint)_pid && (candidate == _window || IsChild(_window, candidate) || IsChild(candidate, _window));
        }

        private string DescribeStartHit(IntPtr receiver)
        {
            // Read-only diagnostics do not grant input authority or change the preflight.
            Func<IntPtr, string> describe = delegate(IntPtr window)
            {
                uint owner; GetWindowThreadProcessId(window, out owner);
                var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
                return "0x" + window.ToInt64().ToString("X") + "/pid=" + owner + "/class=" + name;
            };
            Rect rect; bool hasRect = GetWindowRect(_window, out rect);
            int kind = -1; bool contained = false;
            IntPtr region = CreateRectRgn(0, 0, 0, 0);
            if (region != IntPtr.Zero)
            {
                try { kind = GetWindowRgn(_window, region); contained = hasRect && kind > 0 && PtInRegion(region, _start.X-rect.Left, _start.Y-rect.Top); }
                finally { DeleteObject(region); }
            }
            return "point=" + _start.X + "," + _start.Y + "; input=" + describe(_window) +
                "; rect=" + (hasRect ? rect.Left + "," + rect.Top + "," + rect.Right + "," + rect.Bottom : "unavailable") +
                "; regionKind=" + kind + "; regionContains=" + contained +
                "; windowFromPoint=" + describe(receiver) + "; physical=" + describe(WindowFromPhysicalPoint(_start));
        }

        private void RequireKeyboardDestination()
        {
            IntPtr foreground = GetForegroundWindow();
            uint foregroundOwner;
            uint foregroundThread = GetWindowThreadProcessId(foreground, out foregroundOwner);
            if (foreground == IntPtr.Zero || !IsWindow(foreground) || foregroundThread == 0 || foregroundOwner != (uint)_pid)
                throw new Abort("NotExecuted", "The target process does not own the foreground window; no Escape was sent.");
            var state = new GuiThreadInfo(); state.Size = (uint)Marshal.SizeOf(typeof(GuiThreadInfo));
            if (!GetGUIThreadInfo(foregroundThread, ref state) || state.Focus == IntPtr.Zero || !IsWindow(state.Focus))
                throw new Abort("NotExecuted", "The foreground keyboard focus cannot be proven; no Escape was sent.");
            uint focusOwner;
            uint focusThread = GetWindowThreadProcessId(state.Focus, out focusOwner);
            if (focusThread == 0 || focusOwner != (uint)_pid || GetForegroundWindow() != foreground)
                throw new Abort("NotExecuted", "Keyboard focus belongs to another process or changed during verification; no Escape was sent.");
            _result.KeyboardOwnershipChecks++;
            _result.KeyboardForegroundWindowHandle = foreground.ToInt64();
            _result.KeyboardFocusWindowHandle = state.Focus.ToInt64();
            _steps.Add("keyboard: foreground and focused HWND both verified as target-process owned");
        }

        private bool OwnCapture()
        {
            return _windowThread != 0 && OwnWindow(CaptureWindow(_windowThread));
        }

        private static IntPtr CaptureWindow(uint thread)
        {
            var state = new GuiThreadInfo(); state.Size = (uint)Marshal.SizeOf(typeof(GuiThreadInfo));
            if (!GetGUIThreadInfo(thread, ref state)) throw new Abort("NotExecuted", "The native capture owner cannot be inspected.");
            return state.Capture;
        }

        private void RequireDestination(Point point, bool requireCapture)
        {
            RequireVirtualPoint(point);
            if (OwnCapture()) { _result.OwnedCaptureObserved = true; return; }
            if (requireCapture) throw new Abort("Failed", "Owned capture was lost; no further pointer motion was sent.");
            if (!OwnWindow(WindowFromPoint(point))) throw new Abort("Occluded", "The next pointer destination is obscured or belongs to another window.");
        }

        private void RequireActualPointer(Point expected)
        {
            var wait = Stopwatch.StartNew();
            do
            {
                Point actual;
                if (!GetPhysicalCursorPos(out actual)) throw new Abort("Failed", "The physical cursor became unreadable.");
                if (Math.Abs((long)actual.X - expected.X) <= 1 && Math.Abs((long)actual.Y - expected.Y) <= 1)
                { RequireDestination(actual, false); return; }
                Thread.Sleep(5);
            } while (wait.ElapsedMilliseconds < 40);
            throw new Abort("Failed", "The physical pointer did not reach the requested destination; no click was sent.");
        }

        private static void RequireVirtualPoint(Point point)
        {
            int left = GetSystemMetrics(76), top = GetSystemMetrics(77), width = GetSystemMetrics(78), height = GetSystemMetrics(79);
            if (width <= 1 || height <= 1 || point.X < left || point.Y < top ||
                (long)point.X >= (long)left + width || (long)point.Y >= (long)top + height)
                throw new Abort("NotExecuted", "A pointer destination is outside the physical virtual desktop.");
        }

        private void Move(Point point, bool requireCapture)
        {
            RequireEnvironment(); RequireDestination(point, requireCapture);
            int left = GetSystemMetrics(76), top = GetSystemMetrics(77), width = GetSystemMetrics(78), height = GetSystemMetrics(79);
            var input = new Input(); input.Type = 0;
            input.Data.Mouse.X = (int)Math.Round(((double)point.X - left) * 65535 / (width - 1));
            input.Data.Mouse.Y = (int)Math.Round(((double)point.Y - top) * 65535 / (height - 1));
            input.Data.Mouse.Flags = 0x0001 | 0x8000 | 0x4000;
            Send(input); _result.MouseMoves++;
        }

        private void MouseButton(bool down)
        {
            var input = new Input(); input.Type = 0; input.Data.Mouse.Flags = down ? 0x0002u : 0x0004u;
            Send(input);
            if (down) { _leftOwned = true; _result.LeftDowns++; }
            else { _leftOwned = false; _result.LeftUps++; }
        }

        private void RightButton(bool down)
        {
            var input = new Input(); input.Type = 0; input.Data.Mouse.Flags = down ? 0x0008u : 0x0010u;
            Send(input);
            if (down) { _rightOwned = true; _result.RightDowns++; }
            else { _rightOwned = false; _result.RightUps++; }
        }

        private void Escape(bool down)
        {
            var input = new Input(); input.Type = 1; input.Data.Keyboard.VirtualKey = 0x1B;
            input.Data.Keyboard.Flags = down ? 0u : 2u;
            Send(input);
            if (down) { _escapeOwned = true; _result.EscapeDowns++; }
            else { _escapeOwned = false; _result.EscapeUps++; }
        }

        private void Send(Input input)
        {
            uint delivered = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input)));
            if (delivered != 1) throw new Abort("Failed", "SendInput did not deliver the requested event; Win32 error=" + Marshal.GetLastWin32Error());
            _result.Executed = true;
        }

        private void CleanupRelease(bool escape, bool right = false)
        {
            try
            {
                if (escape) Escape(false); else if (right) RightButton(false); else MouseButton(false);
                _steps.Add(escape ? "cleanup: released driver-owned Escape" : right
                    ? "cleanup: released driver-owned right button" : "cleanup: released driver-owned left button");
            }
            catch (Exception error)
            {
                _result.CleanupSucceeded = false;
                _result.CleanupFailure = (_result.CleanupFailure == null ? "" : _result.CleanupFailure + "; ") + error.Message;
            }
        }
    }

    private static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
    private static bool ModifierDown()
    {
        return Down(0x10) || Down(0x11) || Down(0x12) || Down(0x5B) || Down(0x5C);
    }
    private static string ObjectName(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return null;
        var text = new StringBuilder(256); int needed;
        return GetUserObjectInformation(handle, 2, text, text.Capacity * 2, out needed) ? text.ToString() : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public Rect CaretRect;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPhysicalPoint(Point point);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetPhysicalCursorPos(out Point point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder text, int size, out int needed);
}
