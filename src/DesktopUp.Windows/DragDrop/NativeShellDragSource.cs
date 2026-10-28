using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DesktopUp.Windows;

[ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeFileDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);
    [PreserveSig] int GiveFeedback(uint effect);
}

public sealed record NativeShellDragResult(bool Released, bool ReturnedToOrigin, uint Effect, FileDropPoint Point);

// The Shell target owns file operations, conflicts, progress and desktop placement. Never
// infer a destination from cursor coordinates and never delete a source on a MOVE result.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class NativeShellDragSource : INativeFileDropSource, IDisposable
{
    [ThreadStatic] private static NativeShellDragSource? active;
    private readonly nint origin;
    private readonly Func<bool> current;
    private readonly Action<FileDropPoint> preview;
    private readonly IDataObject data = null!;
    private readonly NativeDragDataObject? ownedData;
    private readonly ShellDropImages images = null!;
    private nint identity;
    private bool overOrigin, returned, released;
    private FileDropPoint last;
    private bool disposed;
    private bool started;
    private uint dragThread;
    private int cancellation;

    public NativeShellDragSource(nint origin, IReadOnlyList<string> paths, Func<bool> current, Action<FileDropPoint> preview)
    {
        if(paths.Count>100 || paths.Any(p=>!Path.IsPathFullyQualified(p)))throw new ArgumentException("A bounded absolute path list is required.");
        this.origin = origin; this.current = current; this.preview = preview;
        try
        {
        images=new();
        // Same-parent files use the real Shell object, as in DeskBox (including .lnk).
        // Mixed parents use standard CF_HDROP. Absolute PIDLs are never passed as children.
        if (paths.Count > 0 && paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
        { ownedData=new(Guid.NewGuid().ToByteArray(),CreateShellData(paths));data=ownedData; }
        else
        {
            ownedData = new(Guid.NewGuid().ToByteArray()); data = ownedData;
            if (paths.Count > 0)
            {
                byte[] names = Encoding.Unicode.GetBytes(string.Join('\0', paths) + "\0\0");
                var drop = new byte[20 + names.Length]; BitConverter.GetBytes(20).CopyTo(drop, 0);
                BitConverter.GetBytes(1).CopyTo(drop, 16); names.CopyTo(drop, 20); ownedData.Put(15, drop);
            }
        }
        identity = Marshal.GetIUnknownForObject(data);
        var format = NativeDragDataObject.Format(unchecked((short)NativeDragDataObject.RegisterClipboardFormat("Preferred DropEffect")));
        var medium = NativeDragDataObject.Allocate(BitConverter.GetBytes(2u));
        try { data.SetData(ref format, ref medium, false); }
        finally { NativeDragDataObject.ReleaseStgMedium(ref medium); }
        }
        catch { Dispose(); throw; }
    }
    public IDataObject Data => data;
    public NativeShellDragResult Run(CancellationToken stopping = default)
    {
        if (active is not null) throw new InvalidOperationException("A native drag already owns this thread.");
        active = this; started=true; dragThread=GetCurrentThreadId();
        using var registration=stopping.Register(Cancel);
        try
        {
            int hr = DoDragDrop(data, this, 1 | 2 | 4, out uint effect);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            return new(released && hr == 0x40100, returned, effect, last);
        }
        finally { active = null; dragThread=0; }
    }
    public void Cancel()
    {
        Interlocked.Exchange(ref cancellation,1);
        // Wake only this source's OLE message loop. No global keyboard injection, no
        // external window/process control; QueryContinueDrag remains the cancellation owner.
        uint thread=dragThread;
        if(thread!=0)_=PostThreadMessage(thread,0x100,0x1B,0);
    }
    public int QueryContinueDrag(bool escape, uint keys)
    {
        try
        {
            if (!GetCursorPos(out last) || escape || Volatile.Read(ref cancellation)!=0 || !current()) return 0x40101;
            if ((keys & 1) == 0) { released = true; return 0x40100; }
            return 0;
        }
        catch { return 0x40101; } // Never unwind through OLE callbacks.
    }
    public int GiveFeedback(uint effect) => 0x40102;

    internal static bool EnterOrigin(nint window, IDataObject incoming, FileDropPoint point, ref uint effect)
    {
        if (active is not { } source || source.origin != window) return false;
        nint incomingIdentity = Marshal.GetIUnknownForObject(incoming);
        bool matches = incomingIdentity == source.identity; Marshal.Release(incomingIdentity);
        if (!matches) return false;
        source.overOrigin = true;
        effect = source.current() ? 2u : 0;
        source.images.Enter(window, incoming, point, effect);
        source.preview(point); return true;
    }
    internal static bool OverOrigin(nint window, FileDropPoint point, ref uint effect)
    {
        if (active is not { overOrigin: true } source || source.origin != window) return false;
        effect = source.current() ? 2u : 0;
        source.images.Over(point, effect); source.preview(point); return true;
    }
    internal static bool LeaveOrigin(nint window)
    {
        if (active is not { overOrigin: true } source || source.origin != window) return false;
        source.overOrigin = false; source.images.Leave(); return true;
    }
    internal static bool DropOrigin(nint window, IDataObject incoming, FileDropPoint point, ref uint effect)
    {
        if (active is not { overOrigin: true } source || source.origin != window) return false;
        source.returned = source.current(); source.last = point; effect = source.returned ? 2u : 0;
        source.images.Drop(incoming, point, effect); source.overOrigin = false; return true;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        images?.Dispose();
        if (identity != 0) { Marshal.Release(identity); identity = 0; }
        if(!started)ownedData?.Dispose(); // After OLE, CCW lifetime owns retained formats.
        if (ownedData is null && data is not null && Marshal.IsComObject(data)) Marshal.FinalReleaseComObject(data);
    }
    private static IDataObject CreateShellData(IReadOnlyList<string> paths)
    {
        nint parent = 0; var absolute = new nint[paths.Count];
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(Path.GetDirectoryName(paths[0])!, 0, out parent, 0, out _));
            var children = new nint[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(paths[i], 0, out absolute[i], 0, out _));
                children[i] = ILFindLastID(absolute[i]);
            }
            var iid = typeof(IDataObject).GUID;
            Marshal.ThrowExceptionForHR(SHCreateDataObject(parent, (uint)children.Length, children, 0, ref iid, out var result));
            return result;
        }
        finally { if (parent != 0) Marshal.FreeCoTaskMem(parent); foreach (var p in absolute) if (p != 0) Marshal.FreeCoTaskMem(p); }
    }
    [DllImport("ole32.dll")] private static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] IDataObject data,
        [MarshalAs(UnmanagedType.Interface)] INativeFileDropSource source, uint allowed, out uint effect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out FileDropPoint point);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread,uint message,nuint wParam,nint lParam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint context, out nint pidl, uint attributes, out uint found);
    [DllImport("shell32.dll")] private static extern nint ILFindLastID(nint pidl);
    [DllImport("shell32.dll")] private static extern int SHCreateDataObject(nint parent, uint count,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] children, nint inner, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IDataObject data);
}
