using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DesktopUp.Application;

namespace DesktopUp.Windows;

[StructLayout(LayoutKind.Sequential)]
public struct FileDropPoint { public int X,Y; }

[ComVisible(true),Guid("00000122-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPhysicalFileDropTarget
{
    [PreserveSig] int DragEnter([MarshalAs(UnmanagedType.Interface)] IDataObject data,uint keys,FileDropPoint point,ref uint effect);
    [PreserveSig] int DragOver(uint keys,FileDropPoint point,ref uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int Drop([MarshalAs(UnmanagedType.Interface)] IDataObject data,uint keys,FileDropPoint point,ref uint effect);
}

// One registration on a host-owned bounded HWND, never a full-screen overlay.
// CF_HDROP strings are snapshotted; no COM object/temporary virtual stream outlives Drop.
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class NativeFileDropTarget : IPhysicalFileDropTarget,IDisposable
{
    private readonly nint window;
    private readonly NativeIncomingDropSession session;
    private IDisposable? lease;
    private bool disposed;
    public bool IsHovering => session.IsHovering;
    internal Action? DropFinished { get; set; }

    // Compatibility path retains append semantics until D3 landing transactions are connected.
    public NativeFileDropTarget(nint window, Func<bool> available, Action<string[]> accept, IResourceLedger ledger, IStructuredLog log)
        : this(window, LegacySurface(window, available), ticket => accept(ticket.Paths.ToArray()), ledger, log, null, false) { }

    public NativeFileDropTarget(nint window, Func<IncomingDropSurface?> surface, Action<IncomingDropTicket> accept,
        IResourceLedger ledger, IStructuredLog log, Action<IncomingDropPreview?>? changed = null)
        : this(window, surface, accept, ledger, log, changed, true) { }

    private NativeFileDropTarget(nint window, Func<IncomingDropSurface?> surface, Action<IncomingDropTicket> accept,
        IResourceLedger ledger, IStructuredLog log, Action<IncomingDropPreview?>? changed, bool useShellImages)
    {
        this.window = window;
        int initialized = OleInitialize(0);
        if (initialized < 0) Marshal.ThrowExceptionForHR(initialized);
        bool registered = false;
        NativeIncomingDropSession? created = null;
        try
        {
            created = new(window, surface, accept, useShellImages ? new ShellDropImages() : new LegacyImages(), log, changed);
            session = created;
            Marshal.ThrowExceptionForHR(RegisterDragDrop(window, this));
            registered = true;
            lease = ledger.Acquire("ole.file-drop-target", "poster-input");
        }
        catch
        {
            if (registered) _ = RevokeDragDrop(window);
            created?.Dispose(); OleUninitialize(); throw;
        }
    }
    private static Func<IncomingDropSurface?> LegacySurface(nint window, Func<bool> available)
    {
        Guid run = Guid.NewGuid();
        return () => available() && GetWindowRect(window, out var r)
            ? new(run, "legacy-append", 0, 1, 0, new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
                1, 1, new(0, 0, 0, 0), DesktopUp.Domain.ContentFlowAxis.Vertical) : null;
    }
    private sealed class LegacyImages : INativeDropImages
    {
        public void Enter(nint window, IDataObject data, FileDropPoint point, uint effect) { }
        public void Over(FileDropPoint point, uint effect) { }
        public void Leave() { }
        public void Drop(IDataObject data, FileDropPoint point, uint effect) { }
        public void Dispose() { }
    }
    public int DragEnter(IDataObject data, uint keys, FileDropPoint point, ref uint effect) => session.DragEnter(data, keys, point, ref effect);
    public int DragOver(uint keys, FileDropPoint point, ref uint effect) => session.DragOver(keys, point, ref effect);
    public int DragLeave() => session.DragLeave();
    public int Drop(IDataObject data, uint keys, FileDropPoint point, ref uint effect)
    {
        try{return session.Drop(data, keys, point, ref effect);}
        finally{try{DropFinished?.Invoke();}catch{ /* never cross the COM boundary */ }}
    }
    public static string[]? ReadPaths(IDataObject data)
    {
        var format=new FORMATETC{cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL};
        if(data.QueryGetData(ref format)!=0)return null;
        data.GetData(ref format,out var medium);
        try
        {
            if(medium.tymed!=TYMED.TYMED_HGLOBAL || medium.unionmember==0)return null;
            uint count=DragQueryFile(medium.unionmember,uint.MaxValue,null,0);
            if(count is 0 or >100)return null;
            var result=new string[count];
            for(uint i=0;i<count;i++)
            {
                uint length=DragQueryFile(medium.unionmember,i,null,0);
                if(length is 0 or >8192)return null;
                var text=new StringBuilder((int)length+1);
                if(DragQueryFile(medium.unionmember,i,text,length+1)!=length)return null;
                result[i]=text.ToString();
            }
            return result;
        }
        finally{ReleaseStgMedium(ref medium);}
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { _ = RevokeDragDrop(window); session.Dispose(); }
        finally { lease?.Dispose(); lease = null; OleUninitialize(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int RegisterDragDrop(nint window,[MarshalAs(UnmanagedType.Interface)] IPhysicalFileDropTarget target);
    [DllImport("ole32.dll")] private static extern int RevokeDragDrop(nint window);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="DragQueryFileW")]
    private static extern uint DragQueryFile(nint drop,uint index,StringBuilder? file,uint length);
}
