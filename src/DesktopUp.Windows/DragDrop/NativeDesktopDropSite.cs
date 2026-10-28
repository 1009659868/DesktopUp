using System.Runtime.InteropServices;
using System.Text;
using DesktopUp.Application;

namespace DesktopUp.Windows;

public static class NativeDesktopDropSite
{
    // Read-only HWND ancestry; no Explorer injection or icon coordinate persistence.
    public static bool IsDesktop(int x,int y)
    {
        nint window=WindowFromPoint(new FileDropPoint{X=x,Y=y});
        if(window==0)return false;
        GetWindowThreadProcessId(GetShellWindow(),out uint shell);
        GetWindowThreadProcessId(window,out uint owner);
        if(shell==0||owner!=shell)return false;
        bool desktopView=false;
        for(int i=0;i<8 && window!=0;i++,window=GetParent(window))
        {
            var name=new StringBuilder(256);_=GetClassName(window,name,name.Capacity);
            if(name.ToString()=="SHELLDLL_DefView")desktopView=true;
            if(name.ToString() is "Progman" or "WorkerW")return desktopView;
            // A different shell UI (taskbar, Explorer folder) is not the desktop.
            if(name.ToString() is "Shell_TrayWnd" or "CabinetWClass")return false;
        }
        return false;
    }
    public static bool MoveModifier => (GetAsyncKeyState(0x11)&0x8000)==0 && (GetAsyncKeyState(0x12)&0x8000)==0;
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(FileDropPoint point);
    [DllImport("user32.dll")] private static extern nint GetParent(nint window);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(nint window,StringBuilder text,int length);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
