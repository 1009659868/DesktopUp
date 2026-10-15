using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DesktopUp.Infrastructure;

// Only constructs the built-in Shell Link object. No Resolve, InvokeCommand, target
// execution or third-party context menu. Serialize first; the caller uses CreateNew.
internal static class ShellShortcutFile
{
    internal static byte[] Encode(string target) => InApartment(() =>
    {
        object link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true)!)!;
        IStream? stream = null;
        try
        {
            ((ILink)link).SetPath(target);
            ((ILink)link).SetWorkingDirectory(Path.GetDirectoryName(target)!);
            Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(0, true, out stream));
            ((IPersistStream)link).Save(stream, true);
            stream.Stat(out var stat, 1);
            if (stat.cbSize is < 1 or > 131072) throw new IOException("快捷方式数据超限。");
            byte[] bytes = new byte[(int)stat.cbSize];
            stream.Seek(0, 0, 0); stream.Read(bytes, bytes.Length, 0);
            return bytes;
        }
        finally { if (stream is not null) Marshal.FinalReleaseComObject(stream); Marshal.FinalReleaseComObject(link); }
    });

    internal static string ReadStoredTarget(string path) => InApartment(() =>
    {
        object link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true)!)!;
        try { ((IPersistFile)link).Load(path, 0); var text = new StringBuilder(8192); ((ILink)link).GetPath(text, text.Capacity, 0, 4); return text.ToString(); }
        finally { Marshal.FinalReleaseComObject(link); }
    });
    private static T InApartment<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { result.SetResult(action()); } catch (Exception e) { result.SetException(e); } })
            { IsBackground = true, Name = "DesktopUp new shortcut" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return result.Task.GetAwaiter().GetResult();
    }
    [DllImport("ole32.dll")] private static extern int CreateStreamOnHGlobal(nint memory, [MarshalAs(UnmanagedType.Bool)] bool delete, out IStream stream);
    [ComImport, Guid("00000109-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistStream
    {
        void GetClassID(out Guid id); [PreserveSig] int IsDirty(); void Load(IStream stream);
        void Save(IStream stream, [MarshalAs(UnmanagedType.Bool)] bool clear); void GetSizeMax(out long size);
    }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ILink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, nint data, uint flags);
        void GetIDList(out nint id); void SetIDList(nint id);
        void GetDescription(nint text, int count); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory(nint text, int count); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetArguments(nint text, int count); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetHotkey(out short key); void SetHotkey(short key); void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation(nint text, int count, out int index); void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved); void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
