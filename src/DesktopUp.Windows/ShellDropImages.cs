using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace DesktopUp.Windows;

public interface INativeDropImages : IDisposable
{
    void Enter(nint window, IDataObject data, FileDropPoint point, uint effect);
    void Over(FileDropPoint point, uint effect);
    void Leave();
    void Drop(IDataObject data, FileDropPoint point, uint effect);
}

// STA owner only. This controls the source's image, not the file executor or source deletion.
public sealed class ShellDropImages : INativeDropImages
{
    private object? instance;
    private readonly TargetHelper target;
    private MissingSourceDragImage? fallback;
    public ShellDropImages()
    {
        instance = CreateIsolatedHelper();
        try { target = (TargetHelper)instance; }
        catch { Marshal.FinalReleaseComObject(instance); instance = null; throw; }
    }
    internal static object CreateIsolatedHelper()
    {
        // Shell may return the same COM identity to multiple STA consumers. A private RCW
        // prevents disposing one poster/helper from disconnecting another consumer's wrapper.
        var clsid = new Guid("4657278A-411B-11D2-839A-00C04FD918D0");
        var iid = new Guid("00000000-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, 0, 1, ref iid, out nint unknown));
        try { return Marshal.GetUniqueObjectForIUnknown(unknown); }
        finally { Marshal.Release(unknown); }
    }
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, out nint value);
    public void Enter(nint window, IDataObject data, FileDropPoint point, uint effect)
    {
        int result = target.DragEnter(window, data, ref point, effect);
        if (result == unchecked((int)0x80040064)) // CF_HDROP without source-image format; no filesystem/Shell extension lookup.
        { _ = target.DragLeave(); fallback = new MissingSourceDragImage(point); return; }
        Marshal.ThrowExceptionForHR(result);
    }
    public void Over(FileDropPoint point, uint effect)
    { if (fallback is not null) fallback.Move(point); else Marshal.ThrowExceptionForHR(target.DragOver(ref point, effect)); }
    public void Leave()
    { if (fallback is not null) { fallback.Dispose(); fallback = null; } else Marshal.ThrowExceptionForHR(target.DragLeave()); }
    public void Drop(IDataObject data, FileDropPoint point, uint effect)
    { if (fallback is not null) { fallback.Dispose(); fallback = null; } else Marshal.ThrowExceptionForHR(target.Drop(data, ref point, effect)); }
    public void Dispose()
    {
        if (instance is null) return;
        fallback?.Dispose(); fallback = null;
        var owned = instance; instance = null;
        Marshal.FinalReleaseComObject(owned);
    }
    [ComImport, Guid("4657278B-411B-11D2-839A-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface TargetHelper
    {
        [PreserveSig] int DragEnter(nint window, [MarshalAs(UnmanagedType.Interface)] IDataObject data, ref FileDropPoint point, uint effect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int DragOver(ref FileDropPoint point, uint effect);
        [PreserveSig] int Drop([MarshalAs(UnmanagedType.Interface)] IDataObject data, ref FileDropPoint point, uint effect);
        [PreserveSig] int Show([MarshalAs(UnmanagedType.Bool)] bool show);
    }
}
