using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using IDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using DesktopUp.Windows;

namespace DesktopUp.NativeDragProbe;

// D1 only: no path mutation, application state, transfer executor or source cleanup.
public interface IProbeDragImages : IDisposable
{
    void Enter(nint window, IDataObject data, FileDropPoint point, uint effect);
    void Over(FileDropPoint point, uint effect);
    void Leave();
    void Drop(IDataObject data, FileDropPoint point, uint effect);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ProbeDropTarget : IPhysicalFileDropTarget, IDisposable
{
    private readonly nint window;
    private readonly IProbeDragImages images;
    private readonly Action<string, object?> record;
    private readonly Func<bool> enabled;
    private string? fingerprint;
    private bool entered, disposed;
    public int ObservedDrops { get; private set; }
    public bool Active => entered;

    public ProbeDropTarget(nint window, IProbeDragImages images, Action<string, object?> record, Func<bool> enabled)
    { this.window = window; this.images = images; this.record = record; this.enabled = enabled; }

    private static string? Read(IDataObject data)
    {
        if (ProbeDataObject.TryReadBytes(data, ProbeDataObject.PayloadFormat, out var value))
            return "private:" + Convert.ToHexString(value);
        var paths = NativeFileDropTarget.ReadPaths(data);
        // Memory only. Logs never include external paths. Payload is not executed.
        return paths is { Length: > 0 } ? "hdrop:" + string.Join('\0', paths) : null;
    }
    private uint Feedback(uint keys, uint offered) => !disposed && entered && fingerprint is not null &&
        enabled() && (keys & (8u | 32u | 2u)) == 0 && (offered & 2) != 0 ? 2u : 0u;

    private void Visual(string phase, Action callback)
    {
        try { callback(); }
        catch (Exception error) { record("image-error", new { phase, type = error.GetType().Name, error.Message }); }
    }
    public int DragEnter(IDataObject data, uint keys, FileDropPoint point, ref uint effect)
    {
        if (entered) DragLeave();
        effect = disposed ? 0 : effect;
        if (disposed) return 0;
        entered = true;
        try { fingerprint = Read(data); effect = Feedback(keys, effect); }
        catch (Exception error) { fingerprint = null; effect = 0; record("payload-error", error.Message); }
        var currentEffect = effect;
        Visual("enter", () => images.Enter(window, data, point, currentEffect));
        record("enter", new { point.X, point.Y, effect, payload = fingerprint?.StartsWith("private:") == true ? "private" : fingerprint is null ? "unsupported" : "hdrop" });
        return 0;
    }
    public int DragOver(uint keys, FileDropPoint point, ref uint effect)
    {
        try { effect = Feedback(keys, effect); } catch { effect = 0; }
        var currentEffect = effect;
        if (entered) Visual("over", () => images.Over(point, currentEffect));
        record("over", new { point.X, point.Y, effect });
        return 0;
    }
    public int DragLeave()
    {
        if (entered) Visual("leave", images.Leave);
        entered = false; fingerprint = null;
        record("leave", null);
        return 0;
    }
    public int Drop(IDataObject data, uint keys, FileDropPoint point, ref uint effect)
    {
        uint offered = effect; effect = 0; bool observed = false;
        try
        {
            observed = Feedback(keys, offered) != 0 && Read(data) == fingerprint;
            if (observed) ObservedDrops++;
        }
        catch (Exception error) { record("payload-error", error.Message); }
        finally
        {
            // Always NONE: a dry run must not authorize an external source to delete.
            if (entered) Visual("drop", () => images.Drop(data, point, 0));
            entered = false; fingerprint = null;
        }
        record("drop", new { point.X, point.Y, observed, returnedEffect = 0, filesMoved = 0 });
        return 0;
    }
    public void Dispose()
    {
        if (disposed) return;
        DragLeave(); disposed = true;
        Visual("dispose", images.Dispose);
    }
}

public static class ProbeOutgoingVerdict
{
    // Shell effect(s) and target CLSID do not identify the resulting file path.
    // Even MOVE+MOVE must never become a delete command in this probe.
    public static string Classify(int hresult, uint returnedEffect, uint? performedEffect) => hresult switch
    {
        0x40101 => "cancelled-no-cleanup",
        < 0 => "failed-no-cleanup",
        0x40100 when returnedEffect == 0 && performedEffect is null => "no-file-receipt-no-cleanup",
        _ => "requires-authoritative-file-receipt-no-cleanup"
    };
}
