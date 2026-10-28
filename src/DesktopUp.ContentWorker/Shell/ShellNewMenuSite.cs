using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.ContentWorker;

// New is a Shell extension, including registered document templates. Supply only
// its documented view callback; never emulate ShellNew registry commands ourselves.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ShellNewMenuSite(string directory) : INewMenuClient, IShellServiceProvider
{
    internal ShellCreatedItem? Created { get; private set; }
    public int IncludeItems(out uint flags) { flags = 3; return 0; } // Folders and non-folders.
    public int SelectAndEditItem(nint pidl, uint flags)
    {
        try
        {
            Marshal.ThrowExceptionForHR(SHGetNameFromIDList(pidl, 0x80058000, out nint name));
            string path;
            try { path = Marshal.PtrToStringUni(name)!; } finally { Marshal.FreeCoTaskMem(name); }
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), StringComparison.OrdinalIgnoreCase))
                return unchecked((int)0x80070057);
            Created = new(path, ShellUiHost.Identity(path), (flags & 1) != 0);
            return 0;
        }
        catch { return unchecked((int)0x80004005); }
    }
    public int QueryService(ref Guid service, ref Guid iid, out nint result)
    {
        result = 0;
        if(service != typeof(INewMenuClient).GUID) return unchecked((int)0x80004002);
        nint self = Marshal.GetIUnknownForObject(this);
        try { return Marshal.QueryInterface(self, in iid, out result); }
        finally { Marshal.Release(self); }
    }
    [DllImport("shell32.dll")] private static extern int SHGetNameFromIDList(nint pidl, uint form, out nint name);
}
[ComVisible(true), Guid("dcb07fdc-3bb5-451c-90be-966644fed7b0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INewMenuClient
{
    [PreserveSig] int IncludeItems(out uint flags);
    [PreserveSig] int SelectAndEditItem(nint pidl, uint flags);
}
[ComVisible(true), Guid("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellServiceProvider
{
    [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out nint result);
}
[ComImport, Guid("fc4801a3-2ba9-11cf-a229-00aa003d7352"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectWithSite
{
    [PreserveSig] int SetSite([MarshalAs(UnmanagedType.IUnknown)] object? site);
    [PreserveSig] int GetSite(ref Guid iid, out nint result);
}
