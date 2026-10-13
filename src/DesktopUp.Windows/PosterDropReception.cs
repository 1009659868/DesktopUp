using DesktopUp.Application;

namespace DesktopUp.Windows;

public interface IPosterDropRegistration : IDisposable
{
    bool IsReady { get; }
    bool IsHovering { get; }
}

/// <summary>
/// Optional OLE registration on the host's bounded input HWND. The host owns the
/// window, geometry and ordinary input; this component owns only the drop session.
/// No separate interception window or global drag detection is needed.
/// </summary>
internal sealed class PosterDropReception : IPosterDropRegistration
{
    private readonly Func<IncomingDropSurface?> surface;
    private NativeFileDropTarget? target;
    private DesktopPixelRect frame;
    private bool disposed, suspended;

    public bool IsReady => !disposed && target is not null;
    public bool IsHovering => target?.IsHovering == true;

    internal PosterDropReception(nint window, DesktopPixelRect frame,
        Func<IncomingDropSurface?> surface, Action<IncomingDropTicket> accept,
        IResourceLedger ledger, IStructuredLog log, Action<IncomingDropPreview?>? changed)
    {
        this.surface = surface;
        this.frame = frame;
        target = new NativeFileDropTarget(window, CurrentSurface, accept, ledger, log, changed);
        log.Write("info", "poster.drop_component_created", new
        {
            handle = $"0x{window:X}", frame, ordinaryInput = "bounded-full-frame"
        });
    }

    private IncomingDropSurface? CurrentSurface() =>
        IsReady && !suspended ? surface() : null;

    internal void ApplyFrame(DesktopPixelRect next)
    {
        if (disposed) return;
        if (!next.IsUsable) { Suspend(); return; }
        if (frame != next) target?.DragLeave();
        frame = next;
        suspended = false;
    }

    internal void Suspend()
    {
        if (disposed) return;
        suspended = true;
        target?.DragLeave();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        suspended = true;
        var registration = target;
        target = null;
        registration?.Dispose();
    }
}
