using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

/// <summary>Identical visible and input HRGN; callers own returned handles until SetWindowRgn succeeds.</summary>
internal static class NativeSurfaceRegion
{
    internal static nint Create(DesktopRoundedRegion shape) => shape.IsEmpty ? CreateRectRgn(0, 0, 0, 0) :
        !shape.IsValid ? 0 : shape.Shape switch
        {
            "ellipse" => CreateEllipticRgn(0, 0, shape.Width + 1, shape.Height + 1),
            "rectangle" => CreateRectRgn(0, 0, shape.Width, shape.Height),
            _ => CreateRoundRectRgn(0, 0, shape.Width + 1, shape.Height + 1, shape.CornerRadius * 2, shape.CornerRadius * 2)
        };
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern nint CreateEllipticRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
}
