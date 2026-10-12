using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using IDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace DesktopUp.NativeDragProbe;

[ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IProbeDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);
    [PreserveSig] int GiveFeedback(uint effect);
}
[ComVisible(true), Guid("0000012B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IProbeSourceNotify
{
    [PreserveSig] int DragEnterTarget(nint window);
    [PreserveSig] int DragLeaveTarget();
}
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ProbeSource(Action<string, object?> record, Func<bool> cancelled) : IProbeDropSource, IProbeSourceNotify
{
    public int QueryContinueDrag(bool escape, uint keys) => escape || cancelled() ? 0x40101 : (keys & 1) == 0 ? 0x40100 : 0;
    public int GiveFeedback(uint effect) => 0x40102;
    public int DragEnterTarget(nint window) { record("source-enter-target", new { window = $"0x{window:X}" }); return 0; }
    public int DragLeaveTarget() { record("source-leave-target", null); return 0; }
    [DllImport("ole32.dll")] public static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] IDataObject data, [MarshalAs(UnmanagedType.Interface)] IProbeDropSource source, uint allowedEffects, out uint effect);
}
