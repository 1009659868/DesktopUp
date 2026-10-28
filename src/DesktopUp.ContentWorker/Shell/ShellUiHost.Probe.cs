using System.Runtime.InteropServices;
using System.Text;

namespace DesktopUp.ContentWorker;

internal static partial class ShellUiHost
{
    // Read-only native preparation probe. Initialize dynamic submenus through the
    // same owner-window callback as a displayed menu, without selecting a command.
    private static IReadOnlyDictionary<string,uint> InspectMenu(nint menu, nint owner, IContextMenu shell, int depth = 0, Dictionary<string,uint>? commands = null)
    {
        commands ??= new(StringComparer.OrdinalIgnoreCase);
        if (depth > 2) return commands;
        if (depth == 0) Console.Error.WriteLine($"interfaces: menu3={active3 is not null}; menu2={active2 is not null}");
        for (int index = 0; index < Math.Min(GetMenuItemCount(menu), 128); index++)
        {
            var text = new StringBuilder(512);
            GetMenuString(menu, (uint)index, text, text.Capacity, 0x400);
            nint child = GetSubMenu(menu, index);
            uint id=GetMenuItemID(menu,index);
            nint verb=Marshal.AllocCoTaskMem(1024);
            try
            {
                Marshal.WriteInt16(verb,0);
                int hr=id is >0 and <0x8000?shell.GetCommandString(id-1,4,0,verb,512):-1;
                Console.Error.WriteLine($"{new string(' ', depth)}{text}: id={id}, children={GetMenuItemCount(child)}, verb={(hr>=0?Marshal.PtrToStringUni(verb):"?")}");
                if(hr>=0 && Marshal.PtrToStringUni(verb) is {Length:>0} canonical) commands.TryAdd(canonical,id);
            }
            finally {Marshal.FreeCoTaskMem(verb);}
            if (child == 0) continue;
            SendMessage(owner, 0x117, (nuint)child, index);
            InspectMenu(child, owner, shell, depth + 1, commands);
        }
        return commands;
    }
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")] private static extern nint GetSubMenu(nint menu, int index);
    [DllImport("user32.dll")] private static extern uint GetMenuItemID(nint menu, int index);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetMenuString(nint menu,uint item,StringBuilder text,int maximum,uint flags);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint owner,uint message,nuint wParam,nint lParam);
}
