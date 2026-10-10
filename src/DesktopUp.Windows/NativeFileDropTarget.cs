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

// Registered on the existing shaped input HWND, never on a full-screen overlay.
// CF_HDROP strings are snapshotted; no COM object/temporary virtual stream outlives Drop.
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class NativeFileDropTarget : IPhysicalFileDropTarget,IDisposable
{
    private readonly nint window;
    private readonly Func<bool> available;
    private readonly Action<string[]> accept;
    private readonly IStructuredLog log;
    private IDisposable? lease;
    private string[]? paths;
    private bool disposed;
    public bool IsHovering=>paths is not null&&!disposed;
    public NativeFileDropTarget(nint window,Func<bool> available,Action<string[]> accept,IResourceLedger ledger,IStructuredLog log)
    {
        this.window=window;this.available=available;this.accept=accept;this.log=log;
        int initialized=OleInitialize(0);
        if(initialized<0)Marshal.ThrowExceptionForHR(initialized);
        try { Marshal.ThrowExceptionForHR(RegisterDragDrop(window,this));lease=ledger.Acquire("ole.file-drop-target","poster-input"); }
        catch { OleUninitialize();throw; }
    }
    private bool Allowed(uint keys,uint effects) => !disposed && paths is {Length:>0} && (effects&2)!=0 &&
        // Control/Alt/right-button intentions must never silently become Move.
        (keys&(8u|32u|2u))==0 && available();
    public int DragEnter(IDataObject data,uint keys,FileDropPoint point,ref uint effect)
    {
        try { paths=ReadPaths(data);effect=Allowed(keys,effect)?2u:0; }
        catch(Exception e){paths=null;effect=0;log.Write("warning","transfer.payload_refused",new {e.Message});}
        return 0;
    }
    public int DragOver(uint keys,FileDropPoint point,ref uint effect)
    {try{effect=Allowed(keys,effect)?2u:0;}catch{effect=0;}return 0;}
    public int DragLeave(){paths=null;return 0;}
    public int Drop(IDataObject data,uint keys,FileDropPoint point,ref uint effect)
    {
        uint offered=effect;effect=0;
        try
        {
            if(!Allowed(keys,offered))return 0;
            var snapshot=ReadPaths(data);
            if(snapshot is null || paths is null || !snapshot.SequenceEqual(paths,StringComparer.OrdinalIgnoreCase))return 0;
            paths=null;accept(snapshot);
            // We queued OUR journaled move. NONE deliberately authorizes no source deletion.
            // Completion is published by the application, never inferred from Shell's effect.
        }
        catch(Exception e){log.Write("warning","transfer.drop_refused",new {e.Message});}
        finally{paths=null;}
        return 0;
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
        if(disposed)return;disposed=true;paths=null;
        _=RevokeDragDrop(window);lease?.Dispose();lease=null;OleUninitialize();
    }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int RegisterDragDrop(nint window,[MarshalAs(UnmanagedType.Interface)] IPhysicalFileDropTarget target);
    [DllImport("ole32.dll")] private static extern int RevokeDragDrop(nint window);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="DragQueryFileW")]
    private static extern uint DragQueryFile(nint drop,uint index,StringBuilder? file,uint length);
}
