using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.ContentWorker;

internal static partial class Program
{
    // The bounded disposable STA worker is the only caller. Never load a document thumbnail,
    // paint a Shell background, or inflate a small resource and call it a high-resolution icon.
    private static (byte[] Pixels, int Width, int Height)? TryLargeShellIcon(string path)
    {
        IShellItemImageFactory? factory = null;
        nint bitmap = 0, dc = 0;
        try
        {
            Guid iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, 0, ref iid, out factory) < 0 || factory is null) return null;
            const uint iconOnly = 4; // SIIGBF_ICONONLY | SIIGBF_RESIZETOFIT; no SCALEUP or ICONBACKGROUND
            int maximum = LocalContentProtocol.MaximumIconDimension;
            if (factory.GetImage(new NativeSize { Width = maximum, Height = maximum }, iconOnly, out bitmap) < 0 || bitmap == 0 ||
                GetObjectBitmap(bitmap, Marshal.SizeOf<NativeBitmap>(), out var size) == 0 ||
                size.Width is <= 0 or > LocalContentProtocol.MaximumIconDimension || size.Height is <= 0 or > LocalContentProtocol.MaximumIconDimension)
                return null;
            dc = CreateCompatibleDC(0);
            if (dc == 0) return null;
            var header = new BitmapInfo { Size = 40, Width = size.Width, Height = -size.Height, Planes = 1, BitCount = 32 };
            var pixels = new byte[checked(size.Width * size.Height * 4)];
            // GetDIBits requires the source bitmap NOT to be selected into a device context.
            if (GetDIBits(dc, bitmap, 0, (uint)size.Height, pixels, ref header, 0) != size.Height) return null;
            bool visible = false;
            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                byte alpha = pixels[offset + 3]; visible |= alpha != 0;
                for (int channel = 0; channel < 3; channel++) pixels[offset + channel] = Math.Min(pixels[offset + channel], alpha);
            }
            // Legacy mask-only icons may have no usable alpha; retain the dual-background fallback.
            return visible ? (pixels, size.Width, size.Height) : null;
        }
        catch (Exception error) when (error is COMException or ArgumentException or InvalidCastException or OverflowException)
        { return null; }
        finally
        {
            if (bitmap != 0) DeleteObject(bitmap);
            if (dc != 0) DeleteDC(dc);
            if (factory is not null) Marshal.FinalReleaseComObject(factory);
        }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(NativeSize size, uint flags, out nint bitmap);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeBitmap
    {
        public int Type, Width, Height, WidthBytes;
        public ushort Planes, BitsPixel;
        public nint Bits;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObjectBitmap(nint bitmap, int size, out NativeBitmap info);
    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] pixels, ref BitmapInfo info, uint usage);
}
