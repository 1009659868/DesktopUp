using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Reads the applied native HRGN, not just the managed hit list. No input is sent.
    private object ReadSparseInputForProbe()
    {
        var surface = inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("No published input surface.");
        nint region = SparseCreateRectRgn(0, 0, 0, 0);
        if (region == 0) throw new InvalidOperationException("Cannot allocate region readback.");
        try
        {
            int result = SparseGetWindowRgn(_inputIsland!.WindowHandle, region);
            if (result == 0) throw new InvalidOperationException("Cannot read current native input region.");
            int gaps = 0, hits = 0, ownedGapReceivers = 0;
            var samples = new List<object>();
            // Avoid the anti-aliased rounded boundary; it is covered by native geometry contracts.
            for (int y = 12; y < surface.Frame.Height - 12; y += 17)
            for (int x = 12; x < surface.Frame.Width - 12; x += 17)
            {
                bool expected = surface.Layout.Regions.Any(r => PosterInputGeometry.CapturesPointer(r) &&
                    x >= r.Bounds.X && y >= r.Bounds.Y && x < r.Bounds.Right && y < r.Bounds.Bottom);
                bool actual = SparsePtInRegion(region, x, y);
                if (actual != expected) throw new InvalidOperationException($"Native input region mismatch at {x},{y}; expected={expected}, actual={actual}.");
                if (expected) { hits++; continue; }
                gaps++;
                var point = new SparsePoint { X = surface.Frame.X + x, Y = surface.Frame.Y + y };
                nint receiver = SparseWindowFromPhysicalPoint(point);
                nint root = receiver == 0 ? 0 : SparseGetAncestor(receiver, 2);
                SparseGetWindowThreadProcessId(receiver, out uint processId);
                if (root == _windowHandle || root == _inputIsland.WindowHandle) ownedGapReceivers++;
                if (samples.Count < 12) samples.Add(new { point.X, point.Y, Receiver = $"0x{receiver:X}", Root = $"0x{root:X}", processId });
            }
            if (!IsEditing && (gaps == 0 || hits == 0 || ownedGapReceivers != 0))
                throw new InvalidOperationException($"Daily native input must have real holes and item hits: gaps={gaps}, hits={hits}, own-gap-receivers={ownedGapReceivers}.");
            return new { NativeRegion = true, Gaps = gaps, Hits = hits, OwnedGapReceivers = ownedGapReceivers,
                Samples = samples, NativeMouse = false, ExplorerActionsCertified = false };
        }
        finally { SparseDeleteObject(region); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct SparsePoint { public int X, Y; }
    [DllImport("gdi32.dll", EntryPoint = "CreateRectRgn")] private static extern nint SparseCreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll", EntryPoint = "PtInRegion")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SparsePtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")] private static extern bool SparseDeleteObject(nint region);
    [DllImport("user32.dll", EntryPoint = "GetWindowRgn")] private static extern int SparseGetWindowRgn(nint window, nint region);
    [DllImport("user32.dll", EntryPoint = "WindowFromPhysicalPoint")] private static extern nint SparseWindowFromPhysicalPoint(SparsePoint point);
    [DllImport("user32.dll", EntryPoint = "GetAncestor")] private static extern nint SparseGetAncestor(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")] private static extern uint SparseGetWindowThreadProcessId(nint window, out uint processId);
}
