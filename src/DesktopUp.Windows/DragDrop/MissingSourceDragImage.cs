using System.Runtime.InteropServices;

namespace DesktopUp.Windows;

// Only for a source that supplied no drag bitmap. Standard document icon, never a fabricated
// thumbnail; screen-space image-list ownership ends on every leave/drop/dispose path.
internal sealed class MissingSourceDragImage : IDisposable
{
    private nint images;
    private bool dragging;
    internal MissingSourceDragImage(FileDropPoint point)
    {
        nint icon=0;
        try
        {
            var info=new StockIconInfo { Size=(uint)Marshal.SizeOf<StockIconInfo>(),Path="" };
            Marshal.ThrowExceptionForHR(SHGetStockIconInfo(0,0x100,ref info));icon=info.Icon;
            images=ImageList_Create(32,32,0x21,1,1);
            if(images==0 || ImageList_ReplaceIcon(images,-1,icon)<0 || !ImageList_BeginDrag(images,0,16,16))
                throw new IOException("Fallback drag icon unavailable.");
            dragging=true;
            if(!ImageList_DragEnter(GetDesktopWindow(),point.X,point.Y))throw new IOException("Fallback drag icon cannot enter.");
        }
        catch{Dispose();throw;}
        finally{if(icon!=0)_=DestroyIcon(icon);}
    }
    internal void Move(FileDropPoint point)
    {if(!ImageList_DragMove(point.X,point.Y))throw new IOException("Fallback drag icon cannot move.");}
    public void Dispose()
    {
        if(dragging){_=ImageList_DragLeave(GetDesktopWindow());ImageList_EndDrag();dragging=false;}
        if(images!=0){_=ImageList_Destroy(images);images=0;}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct StockIconInfo{public uint Size;public nint Icon;public int SystemIndex,IconIndex;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Path;}
    [DllImport("shell32.dll")]private static extern int SHGetStockIconInfo(uint id,uint flags,ref StockIconInfo info);
    [DllImport("user32.dll")]private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool DestroyIcon(nint icon);
    [DllImport("comctl32.dll")]private static extern nint ImageList_Create(int x,int y,uint flags,int initial,int grow);
    [DllImport("comctl32.dll")]private static extern int ImageList_ReplaceIcon(nint list,int index,nint icon);
    [DllImport("comctl32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ImageList_BeginDrag(nint list,int index,int x,int y);
    [DllImport("comctl32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ImageList_DragEnter(nint window,int x,int y);
    [DllImport("comctl32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ImageList_DragMove(int x,int y);
    [DllImport("comctl32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ImageList_DragLeave(nint window);
    [DllImport("comctl32.dll")]private static extern void ImageList_EndDrag();
    [DllImport("comctl32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ImageList_Destroy(nint list);
}
