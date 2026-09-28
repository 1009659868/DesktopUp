namespace DesktopUp.Application;

// Physical client coordinates, independent of the window's desktop position or template.
public readonly record struct DesktopRoundedRegion(int Width, int Height, int CornerRadius)
{
    public bool IsEmpty => Width == 0 && Height == 0 && CornerRadius == 0;
    public bool IsValid => Width > 0 && Width < int.MaxValue && Height > 0 && Height < int.MaxValue &&
        CornerRadius >= 0 && CornerRadius <= Math.Min(Width, Height) / 2;
}
