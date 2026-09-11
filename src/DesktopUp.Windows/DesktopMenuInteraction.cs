using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed record DesktopMenuSurface(nint Visual, nint Input, Action CancelCapture, Func<bool> RestoreLayer);
public sealed record DesktopMenuHit(int X, int Y, bool HitPopup, string Receiver, bool ReceiverOwnProcess);
public sealed record DesktopMenuProbe(bool Ordered, bool InputsTransparent, IReadOnlyList<DesktopMenuHit> Samples);
internal readonly record struct DesktopMenuZWindow(nint Handle, bool Topmost);

internal interface IDesktopMenuNative
{
    bool Exists(nint window);
    bool IsInputTransparent(nint window);
    bool SetInputTransparent(nint window, bool transparent);
    nint FindPopup(nint owner);
    IReadOnlyList<DesktopMenuZWindow> ReadZOrder();
    bool PlacePopup(nint popup, nint insertAfter);
    IDisposable WatchPopup(nint popup, Action<uint, nint> beforeMessage);
    IReadOnlyList<DesktopMenuHit> SamplePopup(nint popup);
}

// A UI-thread lease over the existing surfaces, not a replacement menu or input router.
// DeskBox's paired interaction lifetime is retained; its temporary TOPMOST pulse is not.
public sealed class DesktopMenuInteraction : IDisposable
{
    [ThreadStatic] private static DesktopMenuInteraction? s_active;
    [ThreadStatic] private static Action? s_activityChanged;
    public static event Action ActivityChanged { add => s_activityChanged += value; remove => s_activityChanged -= value; }
    private readonly IDesktopMenuNative _native;
    private readonly DesktopMenuSurface[] _surfaces;
    private readonly Dictionary<nint, bool> _originalTransparency = [];
    private readonly HashSet<nint> _windows;
    private readonly nint _owner;
    private readonly Action _closeMenu;
    private readonly Action<Action> _enqueue;
    private readonly IStructuredLog _log;
    private IDisposable? _lease;
    private IDisposable? _popupHook;
    private nint _popup;
    private bool _disposed;
    private bool _closing;
    private bool _placing;

    private DesktopMenuInteraction(nint owner, DesktopMenuSurface[] surfaces,
        Action closeMenu, Action<Action> enqueue, IStructuredLog log, IDesktopMenuNative native)
    {
        _owner = owner; _surfaces = surfaces; _closeMenu = closeMenu; _enqueue = enqueue;
        _log = log; _native = native;
        _windows = surfaces.SelectMany(s => new[] { s.Visual, s.Input }).ToHashSet();
    }

    public static DesktopMenuInteraction Begin(nint owner, DesktopMenuSurface[] surfaces,
        Action closeMenu, Action<Action> enqueue, IResourceLedger ledger, IStructuredLog log) =>
        Begin(owner, surfaces, closeMenu, enqueue, ledger, log, new NativeDesktopMenuPlatform());

    internal static DesktopMenuInteraction Begin(nint owner, DesktopMenuSurface[] surfaces,
        Action closeMenu, Action<Action> enqueue, IResourceLedger ledger, IStructuredLog log, IDesktopMenuNative native)
    {
        s_active?.Cancel();
        if (owner == 0 || surfaces.Length == 0 || !surfaces.Any(s => s.Visual == owner))
            throw new ArgumentException("A live poster owner is required.");
        var session = new DesktopMenuInteraction(owner, surfaces, closeMenu, enqueue, log, native);
        try
        {
            // Finish any old capture before freezing the pair order and exposing the Popup.
            foreach (var surface in surfaces) surface.CancelCapture();
            s_active = session;
            session.NotifyActivity();
            session._lease = ledger.Acquire("interaction.poster-menu", "shared-desktop-popup");
            foreach (var surface in surfaces)
            {
                if (!native.Exists(surface.Visual) || !native.Exists(surface.Input))
                    throw new InvalidOperationException("A participating poster was destroyed.");
                session._originalTransparency.Add(surface.Input, native.IsInputTransparent(surface.Input));
                if (!native.SetInputTransparent(surface.Input, true))
                    throw new InvalidOperationException("Cannot release an input island for the menu.");
            }
            log.Write("info", "poster.menu_interaction_prepared", new { owner = $"0x{owner:X}", surfaces = surfaces.Length });
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    public static bool IsActive => s_active is not null;
    public static bool BlocksInput(nint input) => s_active?._originalTransparency.ContainsKey(input) == true;
    public static bool ProtectsSurface(nint window) => s_active?._windows.Contains(window) == true;
    public static void CancelActive() => s_active?.Cancel();
    public static void CancelForSurface(nint window)
    {
        if (ProtectsSurface(window)) s_active?.Cancel();
    }

    public static bool PreserveSurfaceZOrder(nint window, nint lParam)
    {
        if (!ProtectsSurface(window) || lParam == 0) return false;
        var position = Marshal.PtrToStructure<MenuWindowPos>(lParam);
        position.Flags |= 0x0004 | 0x0010 | 0x0200; // NOZORDER | NOACTIVATE | NOOWNERZORDER
        Marshal.StructureToPtr(position, lParam, false);
        return true;
    }

    public bool ActivatePopup()
    {
        if (_disposed || !ReferenceEquals(s_active, this)) return false;
        if (_popupHook is not null) return PlaceAndVerify();
        try
        {
            _popup = _native.FindPopup(_owner);
            if (_popup == 0) throw new InvalidOperationException("The owner's WinUI Popup was not found.");
            _popupHook = _native.WatchPopup(_popup, BeforePopupMessage);
            if (!PlaceAndVerify()) throw new InvalidOperationException("Desktop menu order read-back failed.");
            _log.Write("info", "poster.menu_interaction_active", new { owner = $"0x{_owner:X}", popup = $"0x{_popup:X}", surfaces = _surfaces.Length });
            return true;
        }
        catch (Exception exception)
        {
            _log.Write("warning", "poster.menu_interaction_failed", new { exception.Message });
            Cancel();
            return false;
        }
    }

    // Read-only fixture/diagnostic evidence; queries do not claim actual click delivery.
    public DesktopMenuProbe? ReadProbe() => _disposed || _popup == 0 ? null : new(
        IsOrdered(_native.ReadZOrder(), _windows, _popup),
        _originalTransparency.Keys.All(w => _native.Exists(w) && _native.IsInputTransparent(w)),
        _native.SamplePopup(_popup));

    // Insert immediately before the frontmost poster, never before an ordinary window
    // already above the desktop group. Never use a topmost HWND as an insertion anchor.
    internal static nint? ResolveAnchor(IReadOnlyList<DesktopMenuZWindow> order, IReadOnlySet<nint> surfaces, nint popup)
    {
        nint anchor = 0; // HWND_TOP only when there is no preceding non-topmost window.
        foreach (var window in order)
        {
            if (window.Handle == popup) continue;
            if (surfaces.Contains(window.Handle)) return anchor;
            if (!window.Topmost) anchor = window.Handle;
        }
        return null;
    }

    internal static bool IsOrdered(IReadOnlyList<DesktopMenuZWindow> order, IReadOnlySet<nint> surfaces, nint popup)
    {
        int menu = -1, firstSurface = -1;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].Handle == popup) { if (order[i].Topmost) return false; menu = i; }
            if (firstSurface < 0 && surfaces.Contains(order[i].Handle)) firstSurface = i;
        }
        return menu >= 0 && firstSurface == menu + 1;
    }

    private bool PlaceAndVerify()
    {
        if (_placing || _disposed) return false;
        _placing = true;
        try
        {
            nint? anchor = ResolveAnchor(_native.ReadZOrder(), _windows, _popup);
            return anchor is not null && _native.PlacePopup(_popup, anchor.Value) &&
                IsOrdered(_native.ReadZOrder(), _windows, _popup);
        }
        finally { _placing = false; }
    }

    private void BeforePopupMessage(uint message, nint lParam)
    {
        if (_disposed) return;
        try
        {
            if (message == 0x0082) { _enqueue(Cancel); return; } // WM_NCDESTROY
            if (message == 0x0046 && lParam != 0) // WM_WINDOWPOSCHANGING
            {
                var position = Marshal.PtrToStructure<MenuWindowPos>(lParam);
                if ((position.Flags & 0x0080) != 0) return; // allow hide/close
                nint? anchor = ResolveAnchor(_native.ReadZOrder(), _windows, _popup);
                if (anchor is null) { position.Flags |= 0x0004; _enqueue(Cancel); }
                else { position.InsertAfter = anchor.Value; position.Flags &= ~0x0004u; }
                position.Flags |= 0x0010 | 0x0200;
                Marshal.StructureToPtr(position, lParam, false);
            }
            else if (message == 0x0047 && !_placing && // owner/system reorders can also move owned Popup
                     !IsOrdered(_native.ReadZOrder(), _windows, _popup) && !PlaceAndVerify())
                _enqueue(Cancel);
        }
        catch (Exception exception)
        {
            _log.Write("warning", "poster.menu_layer_guard_failed", new { exception.Message });
            _enqueue(Cancel); // never close/destroy a Popup inside WM_WINDOWPOSCHANGING
        }
    }

    public void Cancel()
    {
        if (_disposed || _closing) return;
        _closing = true;
        try { _closeMenu(); }
        finally { Dispose(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (ReferenceEquals(s_active, this)) s_active = null;
        bool restored = true;
        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception exception) { restored = false; _log.Write("error", "poster.menu_restore_failed", new { exception.Message }); }
        }
        Cleanup(() => _popupHook?.Dispose());
        foreach (var (window, transparent) in _originalTransparency)
            Cleanup(() => { if (_native.Exists(window)) restored &= _native.SetInputTransparent(window, transparent); });
        foreach (var surface in _surfaces)
            Cleanup(() => { if (_native.Exists(surface.Visual) && _native.Exists(surface.Input)) restored &= surface.RestoreLayer(); });
        Cleanup(() => _lease?.Dispose());
        _log.Write(restored ? "info" : "error", "poster.menu_interaction_released", new { restored, surfaces = _surfaces.Length });
        NotifyActivity();
    }

    private void NotifyActivity()
    {
        foreach (Action subscriber in s_activityChanged?.GetInvocationList() ?? [])
            try { subscriber(); }
            catch (Exception exception) { _log.Write("error", "poster.menu_observer_failed", new { exception.Message }); }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MenuWindowPos
    {
        public nint Window, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }
}
