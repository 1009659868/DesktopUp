using System.Runtime.InteropServices;

namespace DesktopUp.ContentWorker;

// SHObjectProperties and context-menu verbs may return before their property sheet does.
// Keep the isolated host/STA alive while Shell holds thread/process references, pumping
// messages for marshalled calls. No extension is loaded into the WinUI application.
internal sealed class ShellUiLifetime : IDisposable
{
    private nint _counter = Marshal.AllocHGlobal(sizeof(int)), _reference;
    internal ShellUiLifetime()
    {
        Marshal.WriteInt32(_counter, 0);
        Marshal.ThrowExceptionForHR(SHCreateThreadRef(_counter, out _reference));
        Marshal.ThrowExceptionForHR(SHSetThreadRef(_reference));
        SHSetInstanceExplorer(_reference);
    }
    internal void Drain()
    {
        // Drop registrations before the host's own reference. References already acquired
        // by Shell remain authoritative; retaining ours here would keep the helper forever.
        SHSetInstanceExplorer(0); SHSetThreadRef(0);
        if (_reference != 0) { Marshal.Release(_reference); _reference = 0; }
        while (Marshal.ReadInt32(_counter) > 0)
        {
            while (PeekMessage(out var message, 0, 0, 0, 1))
            {
                if (message.Id == 0x12) continue;
                TranslateMessage(ref message); DispatchMessage(ref message);
            }
            // A ref release need not post a message, so use a bounded idle wait.
            MsgWaitForMultipleObjectsEx(0, 0, 50, 0x04ff, 4);
        }
    }
    public void Dispose()
    {
        if (_counter == 0) return;
        Drain(); Marshal.FreeHGlobal(_counter); _counter = 0;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("shlwapi.dll")] private static extern int SHCreateThreadRef(nint counter, out nint reference);
    [DllImport("shlwapi.dll")] private static extern int SHSetThreadRef(nint reference);
    [DllImport("shell32.dll")] private static extern void SHSetInstanceExplorer(nint reference);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint handles, uint timeout, uint wake, uint flags);
}
