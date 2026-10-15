using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.ContentWorker;

// Same isolation pattern as DeskBox's native context-menu proxy. COM declarations follow
// the Windows SDK ABI; extensions and owner-drawn callbacks never enter DesktopUp.App.
internal static partial class ShellUiHost
{
    private static IContextMenu2? active2;
    private static IContextMenu3? active3;
    private static readonly WindowProc Procedure = Dispatch;
    internal static int Run()
    {
        if (!Console.IsInputRedirected || !Console.IsOutputRedirected) return 2;
        int initialized = CoInitializeEx(0, 2);
        nint window = 0;
        try
        {
            var buffer = new char[ShellUiProtocol.MaximumCharacters + 1];
            int length = Console.In.ReadBlock(buffer, 0, buffer.Length);
            if (length > ShellUiProtocol.MaximumCharacters) return 3;
            var request = JsonSerializer.Deserialize<ShellUiRequest>(new string(buffer, 0, length));
            if (request is null || !ShellUiProtocol.Valid(request)) return 4;
            var observed = LocalContentReferenceProbe.Inspect(request.Path);
            if (observed.Item is null || request.Action == ShellUiAction.DirectoryMenu && observed.Item.Kind != DesktopUp.Domain.ContentKind.Folder) return 4;
            if (request.Identity is { } identity && Identity(request.Path) != identity) return 4;
            var wc = new WindowClass { Procedure = Procedure, Instance = GetModuleHandle(null), ClassName = "DesktopUp.ShellUi" };
            if (RegisterClass(ref wc) == 0) return 5;
            window = CreateWindowEx(0, wc.ClassName, "DesktopUp", 0, 0, 0, 0, 0, 0, 0, wc.Instance, 0);
            if (window == 0) return 5;
            using var lifetime = new ShellUiLifetime();
            var site = request.Action == ShellUiAction.DirectoryMenu ? new ShellNewMenuSite(request.Path) : null;
            ShellUiResult result;
            if (request.Action == ShellUiAction.Properties)
            {
                Ready();
                SetForegroundWindow(window);
                bool invoked = SHObjectProperties(window, 2, request.Path, null);
                result = new(request.Id, invoked, false, invoked ? null : "Windows未能打开属性。");
            }
            else result = Menu(request, window, site);
            lifetime.Drain();
            if(result.Invoked && site?.Created is { } created) result = result with { CreatedItem = created };
            Console.WriteLine(JsonSerializer.Serialize(result)); Console.Out.Flush();
            return 0;
        }
        catch(Exception e) {Console.Error.WriteLine($"{e.GetType().Name} 0x{e.HResult:X8}: {e.Message}");return 6; }
        finally { active2 = null; active3 = null; if (window != 0) DestroyWindow(window); if (initialized >= 0) CoUninitialize(); }
    }
    private static void Ready() { Console.WriteLine("ready"); Console.Out.Flush(); }
    private static ShellUiResult Menu(ShellUiRequest request, nint window, ShellNewMenuSite? site)
    {
        nint absolute = 0, menu = 0; IShellFolder? parent = null, folder = null; object? context = null;
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(request.Path, 0, out absolute, 0, out _));
            var folderId = typeof(IShellFolder).GUID;
            Marshal.ThrowExceptionForHR(SHBindToParent(absolute, ref folderId, out parent, out nint child));
            var menuId = typeof(IContextMenu).GUID;
            if (request.Action == ShellUiAction.DirectoryMenu)
            {
                Marshal.ThrowExceptionForHR(parent.BindToObject(child, 0, ref folderId, out var bound));
                folder = (IShellFolder)bound;
                Marshal.ThrowExceptionForHR(folder.CreateViewObject(window, ref menuId, out context));
            }
            else Marshal.ThrowExceptionForHR(parent.GetUIObjectOf(window, 1, [child], ref menuId, 0, out context));
            var shell = (IContextMenu)context;
            if (site is not null && context is IObjectWithSite sited) Marshal.ThrowExceptionForHR(sited.SetSite(site));
            active3 = context as IContextMenu3; active2 = active3 is null ? context as IContextMenu2 : null;
            menu = CreatePopupMenu(); if (menu == 0) throw new IOException("Menu creation failed.");
            // No CMF_CANRENAME: rename is an explicit host command, not a view-owned verb.
            Marshal.ThrowExceptionForHR(shell.QueryContextMenu(menu, 0, 1, 0x7fff, request.Action == ShellUiAction.ItemMenu ? 0x84u : 0u));
            Ready(); SetForegroundWindow(window);
            // TPM_NONOTIFY also suppresses WM_INITMENUPOPUP: Shell's New submenu
            // would remain its unexpanded placeholder. RETURNCMD already avoids WM_COMMAND.
            uint command = TrackPopupMenuEx(menu, 0x100 | 0x2 | 0x40, request.X, request.Y, window, 0);
            PostMessage(window, 0, 0, 0);
            if (command == 0) return new(request.Id, false, true);
            if (command > 0x7fff) throw new IOException("Invalid Shell command.");
            if (LocalContentReferenceProbe.Inspect(request.Path).Item is null) return new(request.Id, false, false, "来源已不可用，请重新打开菜单。");
            if (request.Identity is { } identity && Identity(request.Path) != identity)
                return new(request.Id, false, false, "对象已被替换，未执行命令。");
            var invoke = new InvokeInfo { Size = Marshal.SizeOf<InvokeInfo>(), Window = window, Verb = (nint)(command - 1), Show = 1 };
            Marshal.ThrowExceptionForHR(shell.InvokeCommand(ref invoke));
            return new(request.Id, true, false);
        }
        finally
        {
            active2 = null; active3 = null;
            if (menu != 0) DestroyMenu(menu);
            if (context is not null) Marshal.FinalReleaseComObject(context);
            if (folder is not null) Marshal.FinalReleaseComObject(folder);
            if (parent is not null) Marshal.FinalReleaseComObject(parent);
            if (absolute != 0) Marshal.FreeCoTaskMem(absolute);
        }
    }
    private static nint Dispatch(nint window, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (message is 0x117 or 0x120 || (message is 0x2b or 0x2c && wParam == 0))
            {
                if (active3 is not null && active3.HandleMenuMsg2(message, wParam, lParam, out nint result) >= 0) return result;
                if (active2 is not null && message != 0x120 && active2.HandleMenuMsg(message, wParam, lParam) >= 0) return 0;
            }
        }
        catch { /* Never unwind through native menu callbacks. */ }
        return DefWindowProc(window, message, wParam, lParam);
    }
    internal static string Identity(string path)
    {
        using var handle=CreateFile(path,0x80,1|2|4,0,3,0x02200000,0);
        if(handle.IsInvalid)throw new IOException("File identity unavailable.");
        var bytes=new byte[24];
        if(!GetFileInformationByHandleEx(handle,18,bytes,bytes.Length))throw new IOException("File identity unavailable.");
        return Convert.ToHexString(bytes);
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="CreateFileW")]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name,uint access,uint share,nint security,uint disposition,uint flags,nint template);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(Microsoft.Win32.SafeHandles.SafeFileHandle file,int kind,byte[] result,int length);
    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(nint owner, nint context, nint name, nint eaten, nint pidl, nint attributes);
        [PreserveSig] int EnumObjects(nint owner, uint flags, out nint enumerator);
        [PreserveSig] int BindToObject(nint pidl, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
        [PreserveSig] int BindToStorage(nint pidl, nint context, ref Guid iid, out nint result);
        [PreserveSig] int CompareIDs(nint flags, nint first, nint second);
        [PreserveSig] int CreateViewObject(nint owner, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
        [PreserveSig] int GetAttributesOf(uint count, nint children, ref uint attributes);
        [PreserveSig] int GetUIObjectOf(nint owner, uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] children,
            ref Guid iid, nint reserved, [MarshalAs(UnmanagedType.Interface)] out object result);
    }
    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, nint text, uint maximum);
    }
    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, nint text, uint maximum);
        [PreserveSig] int HandleMenuMsg(uint message, nuint wParam, nint lParam);
    }
    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, nint text, uint maximum);
        [PreserveSig] int HandleMenuMsg(uint message, nuint wParam, nint lParam);
        [PreserveSig] int HandleMenuMsg2(uint message, nuint wParam, nint lParam, out nint result);
    }
    [StructLayout(LayoutKind.Sequential)] private struct InvokeInfo
    { public int Size; public uint Mask; public nint Window, Verb, Parameters, Directory; public int Show; public uint HotKey; public nint Icon; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint WindowProc(nint window, uint message, nuint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    { public uint Style; public WindowProc Procedure; public int ClassExtra, WindowExtra; public nint Instance, Icon, Cursor, Background; public string? MenuName; public string ClassName; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint model);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClass(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint extended, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool SHObjectProperties(nint owner, uint type, string path, string? page);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint context, out nint pidl, uint attributes, out uint found);
    [DllImport("shell32.dll")] private static extern int SHBindToParent(nint pidl, ref Guid iid, out IShellFolder folder, out nint child);
}
