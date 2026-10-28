namespace DesktopUp.Domain;

/// <summary>Authoring geometry shared by pointer handles and numeric controls. No platform types.</summary>
public static class CanvasEditingGeometry
{
    public static (double X, double Y) Rotate(double x, double y, double degrees)
    {
        double angle = degrees * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
        return (x * c - y * s, x * s + y * c);
    }
    public static bool Hit(CanvasRect box, double rotation, double x, double y)
    {
        var p = Rotate(x - box.X - box.Width / 2, y - box.Y - box.Height / 2, -rotation);
        return Math.Abs(p.X) <= box.Width / 2 && Math.Abs(p.Y) <= box.Height / 2;
    }
    public static CanvasBox Constrain(CanvasBox b)
    {
        double w = Math.Clamp(b.Width, .01, 1), h = Math.Clamp(b.Height, .01, 1);
        return new(Math.Clamp(b.X, 0, 1 - w), Math.Clamp(b.Y, 0, 1 - h), w, h);
    }
    public static CanvasBox Resize(CanvasBox box, CanvasRect parent, double rotation, int sideX, int sideY, double dx, double dy, bool proportional)
    {
        var delta = Rotate(dx, dy, -rotation);
        double w = box.Width * parent.Width, h = box.Height * parent.Height;
        double nextW = sideX == 0 ? w : Math.Max(parent.Width * .01, w + delta.X * sideX);
        double nextH = sideY == 0 ? h : Math.Max(parent.Height * .01, h + delta.Y * sideY);
        if (proportional && sideX != 0 && sideY != 0)
        { double scale = Math.Max(nextW / w, nextH / h); nextW = w * scale; nextH = h * scale; }
        var center = Rotate((nextW - w) * sideX / 2, (nextH - h) * sideY / 2, rotation);
        return Constrain(new(box.X + (center.X + (w - nextW) / 2) / parent.Width,
            box.Y + (center.Y + (h - nextH) / 2) / parent.Height, nextW / parent.Width, nextH / parent.Height));
    }
}
