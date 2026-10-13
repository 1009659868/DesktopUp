using System.Runtime.InteropServices.ComTypes;
using DesktopUp.Application;

namespace DesktopUp.Windows;

/// <summary>The same callback state machine is used by the registered target and isolated tests.
/// No IDataObject survives a callback. Every image owner ends on Drop, Leave, reentry or disposal.</summary>
public sealed class NativeIncomingDropSession(nint window, Func<IncomingDropSurface?> surface,
    Action<IncomingDropTicket> accept, INativeDropImages images, IStructuredLog log,
    Action<IncomingDropPreview?>? changed = null) : IPhysicalFileDropTarget, IDisposable
{
    private readonly IncomingDropSession session = new();
    private bool imageActive, disposed;
    public bool IsHovering => !disposed && session.Active;

    public int DragEnter(IDataObject data, uint keys, FileDropPoint point, ref uint effect)
    {
        uint offered = effect; effect = 0;
        if (disposed) return 0;
        End();
        if (disposed) return 0;
        try
        {
            var preview = session.Enter(NativeFileDropTarget.ReadPaths(data), surface(), Point(point), keys, offered);
            changed?.Invoke(preview);
            effect = preview is null ? 0u : 2u;
            imageActive = true; // Also pair a partially failed helper Enter with Leave.
            images.Enter(window, data, point, effect);
        }
        catch (Exception e) { effect = 0; Failed(e); End(); }
        return 0;
    }
    public int DragOver(uint keys, FileDropPoint point, ref uint effect)
    {
        uint offered = effect; effect = 0;
        if (disposed) return 0;
        try
        {
            var preview = session.Over(surface(), Point(point), keys, offered);
            changed?.Invoke(preview);
            effect = preview is null ? 0u : 2u;
            if (imageActive) images.Over(point, effect); // Even a refused point keeps its icon.
        }
        catch (Exception e) { effect = 0; Failed(e); End(); }
        return 0;
    }
    public int DragLeave() { End(); return 0; }
    public int Drop(IDataObject data, uint keys, FileDropPoint point, ref uint effect)
    {
        uint offered = effect; effect = 0;
        if (disposed) return 0;
        IncomingDropTicket? ticket = null;
        try { ticket = session.Drop(NativeFileDropTarget.ReadPaths(data), surface(), Point(point), keys, offered); }
        catch (Exception e) { Failed(e); }
        finally
        {
            session.Leave();
            if (imageActive)
            {
                imageActive = false;
                try { images.Drop(data, point, 0); }
                catch (Exception e) { ticket = null; Failed(e); try { images.Leave(); } catch (Exception cleanup) { Failed(cleanup); } }
            }
            try { changed?.Invoke(null); } catch (Exception e) { ticket = null; Failed(e); }
        }
        // Application owns the journaled move. Never return MOVE and authorize Shell cleanup.
        if (ticket is not null && !disposed) try { accept(ticket); } catch (Exception e) { Failed(e); }
        return 0;
    }
    private static PointerScreenPoint Point(FileDropPoint point) => new(point.X, point.Y);
    private void End()
    {
        session.Leave();
        if (imageActive)
        {
            imageActive = false;
            try { images.Leave(); } catch (Exception e) { Failed(e); }
        }
        try { changed?.Invoke(null); } catch (Exception e) { Failed(e); }
    }
    private void Failed(Exception e)
    {
        try { log.Write("warning", "transfer.native_session_refused", new { e.Message }); } catch { /* COM boundary */ }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; End();
        try { images.Dispose(); } catch (Exception e) { Failed(e); }
    }
}
