using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopUp.Windows;

// A small visual-only sprite, NOT an OLE source/target. The existing pointer capture owns
// the gesture and the journal owns file execution. No input window or Explorer mutation.
public sealed class NativeContentDragImage : IDisposable
{
    private nint window, bitmap, memory, previous;
    private readonly int offsetX, offsetY;
    private readonly int width, height;
    private readonly byte[] pixels;
    public NativeContentDragImage(byte[] bgra, int width, int height, int grabX, int grabY, int x, int y)
    {
        if (width is <1 or >256 || height is <1 or >256 || bgra.Length != width*height*4)
            throw new ArgumentException("Invalid bounded drag pixels.");
        offsetX=grabX;offsetY=grabY;this.width=width;this.height=height;pixels=bgra.ToArray();
        nint screen=GetDC(0);
        try
        {
            // STATIC needs no custom WndProc. HTTRANSPARENT / NOACTIVATE + no activation
            // on show keep desktop hit classification independent of this sprite.
            window=CreateWindowEx(0x00080000|0x00000020|0x08000000|0x00000080|0x00000008,
                "STATIC","DesktopUp drag image",0x80000000,0,0,width,height,0,0,0,0);
            memory=CreateCompatibleDC(screen);
            var info=new BitmapInfo { Size=40,Width=width,Height=-height,Planes=1,BitCount=32 };
            bitmap=CreateDIBSection(screen,ref info,0,out nint bits,0,0);
            if(window==0 || memory==0 || bitmap==0)throw new Win32Exception(Marshal.GetLastPInvokeError());
            Marshal.Copy(bgra,0,bits,bgra.Length);previous=SelectObject(memory,bitmap);
            var dest=new Point(x-offsetX,y-offsetY);var source=new Point(0,0);var size=new Point(width,height);
            var blend=new Blend { Alpha=225,Format=1 };
            if(!UpdateLayeredWindow(window,screen,ref dest,ref size,memory,ref source,0,ref blend,2))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            _=ShowWindow(window,4);
        }
        catch { Dispose();throw; }
        finally { if(screen!=0)_=ReleaseDC(0,screen); }
    }
    public void InitializeShell(System.Runtime.InteropServices.ComTypes.IDataObject data, ShellDropImages helper)
    {
        // IDragSourceHelper consumes its bitmap on success and premultiplies alpha itself.
        // Do not give it the selected layered-window bitmap or premultiply edges twice.
        var straight=pixels.ToArray();
        for(int i=0;i<straight.Length;i+=4)
            if(straight[i+3] is >0 and <255)
                for(int c=0;c<3;c++)straight[i+c]=(byte)Math.Min(255,(straight[i+c]*255+straight[i+3]/2)/straight[i+3]);
        var info=new BitmapInfo {Size=40,Width=width,Height=-height,Planes=1,BitCount=32};
        nint copy=CreateDIBSection(0,ref info,0,out nint bits,0,0);
        if(copy==0)throw new Win32Exception(Marshal.GetLastPInvokeError());
        try {Marshal.Copy(straight,0,bits,straight.Length);helper.InitializeSource(copy,width,height,offsetX,offsetY,data);copy=0;}
        finally {if(copy!=0)_=DeleteObject(copy);}
    }
    public void Move(int x,int y)
    {
        if(window!=0 && !SetWindowPos(window,new nint(-1),x-offsetX,y-offsetY,0,0,0x0010|0x0001))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    public void Dispose()
    {
        if(window!=0){_=DestroyWindow(window);window=0;}
        if(memory!=0 && previous!=0){_=SelectObject(memory,previous);previous=0;}
        if(bitmap!=0){_=DeleteObject(bitmap);bitmap=0;}
        if(memory!=0){_=DeleteDC(memory);memory=0;}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Point(int x,int y){public int X=x,Y=y;}
    [StructLayout(LayoutKind.Sequential)]private struct Blend{public byte Op,Flags,Alpha,Format;}
    [StructLayout(LayoutKind.Sequential)]private struct BitmapInfo
    {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,ImageSize;public int XPels,YPels;public uint Used,Important;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern nint CreateWindowEx(uint ex,string cls,string name,uint style,int x,int y,int w,int h,nint parent,nint menu,nint instance,nint param);
    [DllImport("user32.dll")]private static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll")]private static extern bool ShowWindow(nint h,int cmd);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetWindowPos(nint h,nint after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool UpdateLayeredWindow(nint h,nint dest,ref Point pos,ref Point size,nint source,ref Point sourcePos,uint key,ref Blend blend,uint flags);
    [DllImport("user32.dll")]private static extern nint GetDC(nint h);
    [DllImport("user32.dll")]private static extern int ReleaseDC(nint h,nint dc);
    [DllImport("gdi32.dll")]private static extern nint CreateCompatibleDC(nint h);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(nint h);
    [DllImport("gdi32.dll")]private static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll",SetLastError=true)]private static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint bits,nint section,uint offset);
}
