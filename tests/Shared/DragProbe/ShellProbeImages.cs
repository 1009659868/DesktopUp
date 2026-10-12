using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using IDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using DesktopUp.Windows;

namespace DesktopUp.NativeDragProbe;

public sealed class ShellProbeImages : IProbeDragImages
{
    private object? instance;
    private readonly TargetHelper target;
    public ShellProbeImages()
    {
        instance = ShellDropImages.CreateIsolatedHelper();
        target = (TargetHelper)instance;
    }
    public void Enter(nint window, IDataObject data, FileDropPoint point, uint effect) => Marshal.ThrowExceptionForHR(target.DragEnter(window, data, ref point, effect));
    public void Over(FileDropPoint point, uint effect) => Marshal.ThrowExceptionForHR(target.DragOver(ref point, effect));
    public void Leave() => Marshal.ThrowExceptionForHR(target.DragLeave());
    public void Drop(IDataObject data, FileDropPoint point, uint effect) => Marshal.ThrowExceptionForHR(target.Drop(data, ref point, effect));
    public void InitializeSource(nint bitmap, int width, int height, int x, int y, IDataObject data)
    {
        var image = new DragImage { Width = width, Height = height, X = x, Y = y, Bitmap = bitmap, ColorKey = 0xFFFFFFFF };
        Marshal.ThrowExceptionForHR(((SourceHelper)instance!).InitializeFromBitmap(ref image, data));
    }
    public void Dispose()
    {
        if (instance is null) return;
        Marshal.FinalReleaseComObject(instance); instance = null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DragImage { public int Width, Height, X, Y; public nint Bitmap; public uint ColorKey; }
    [ComImport, Guid("4657278B-411B-11D2-839A-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface TargetHelper
    {
        [PreserveSig] int DragEnter(nint window, [MarshalAs(UnmanagedType.Interface)] IDataObject data, ref FileDropPoint point, uint effect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int DragOver(ref FileDropPoint point, uint effect);
        [PreserveSig] int Drop([MarshalAs(UnmanagedType.Interface)] IDataObject data, ref FileDropPoint point, uint effect);
        [PreserveSig] int Show([MarshalAs(UnmanagedType.Bool)] bool show);
    }
    [ComImport, Guid("DE5BF786-477A-11D2-839D-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface SourceHelper
    {
        [PreserveSig] int InitializeFromBitmap(ref DragImage image, [MarshalAs(UnmanagedType.Interface)] IDataObject data);
        [PreserveSig] int InitializeFromWindow(nint window, ref FileDropPoint point, [MarshalAs(UnmanagedType.Interface)] IDataObject data);
    }
}

public sealed class NullProbeImages : IProbeDragImages
{
    public void Enter(nint window, IDataObject data, FileDropPoint point, uint effect) { }
    public void Over(FileDropPoint point, uint effect) { }
    public void Leave() { }
    public void Drop(IDataObject data, FileDropPoint point, uint effect) { }
    public void Dispose() { }
}
