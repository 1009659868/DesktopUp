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
    // Optional observations keep fakes/backends compatible and never decide placement.
    int? LastPlacementError => null;
    string? LastPlacementFailure => null;
    object DescribeWindow(nint window) => new { handle = $"0x{window:X}", unavailable = true };
    bool IsOwnUiWindow(nint window) => false;
    bool IsOwnedPopup(nint popup, nint owner) => false;
    bool PlacePopupAfterOwnWindow(nint popup, nint input, nint owner) => false;
    bool PlaceInputAfterPopup(nint input, nint popup, nint owner) => false;
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
    private nint _popupPlacementAnchor;
    private nint _permittedInput;

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
        var active = s_active!;
        bool permitted = active._placing && active._permittedInput == window && position.Window == window &&
            position.InsertAfter == active._popup && (position.Flags & 0x0003) == 0x0003 &&
            (position.Flags & (0x0004 | 0x0040 | 0x0080)) == 0; // no geometry, show/hide or no-Z request
        position.Flags |= 0x0010 | 0x0200; // NOACTIVATE | NOOWNERZORDER
        if (!permitted) position.Flags |= 0x0004; // Every other surface operation stays frozen.
        Marshal.StructureToPtr(position, lParam, false);
        return true;
    }

    public bool ActivatePopup()
    {
        if (_disposed || !ReferenceEquals(s_active, this)) return false;
        try
        {
            if (_popupHook is null)
            {
                _popup = _native.FindPopup(_owner);
                if (_popup == 0) throw new InvalidOperationException("The owner's WinUI Popup was not found.");
                _popupHook = _native.WatchPopup(_popup, BeforePopupMessage);
            }
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

    // The first surface must be one of our paired input islands. Foreign windows are never
    // insertion anchors; a visual-first or protected/topmost group is rejected unchanged.
    internal static nint? ResolveOwnInputAnchor(IReadOnlyList<DesktopMenuZWindow> order,
        IReadOnlySet<nint> surfaces, IReadOnlySet<nint> inputs, nint popup)
    {
        foreach (var window in order)
        {
            if (window.Handle == popup) continue;
            if (surfaces.Contains(window.Handle))
                return !window.Topmost && inputs.Contains(window.Handle) ? window.Handle : null;
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
        if (_placing || _disposed)
        {
            _log.Write("warning", "poster.menu_order_readback", new
            { failure = _disposed ? "disposed" : "reentrant", owner = $"0x{_owner:X}", popup = $"0x{_popup:X}" });
            return false;
        }
        _placing = true;
        try
        {
            var before = _native.ReadZOrder().ToArray();
            nint? anchor = ResolveOwnInputAnchor(before, _windows, _surfaces.Select(s => s.Input).ToHashSet(), _popup);
            var beforeEvidence = CaptureOrderEvidence(before, anchor);
            object? popupPlacement = null;
            bool Result(string? failure, string stage, IReadOnlyList<DesktopMenuZWindow> after,
                bool? placed = null, int? nativeError = null, string? nativeFailure = null)
            {
                bool accepted = failure is null;
                _log.Write(accepted ? "info" : "warning", "poster.menu_order_readback", new
                {
                    failure, stage, owner = $"0x{_owner:X}", popup = $"0x{_popup:X}", anchor = anchor is { } a ? $"0x{a:X}" : null,
                    placementAttempted = placed.HasValue, placed, nativeError, nativeFailure, popupPlacement,
                    ordered = IsOrdered(after, _windows, _popup), accepted,
                    outsidePopupOrderUnchanged = SameOutsidePopup(before, after, _popup),
                    before = beforeEvidence, after = CaptureOrderEvidence(after, anchor)
                });
                return accepted;
            }
            if (!_native.IsOwnedPopup(_popup, _owner) ||
                !before.Any(w => w.Handle == _popup && !w.Topmost) ||
                !_windows.All(w => _native.IsOwnUiWindow(w) && before.Any(z => z.Handle == w && !z.Topmost)) ||
                !_surfaces.All(s => _native.IsInputTransparent(s.Input)))
                return Result("own-surface-or-popup-invalid", "preflight", before);
            if (IsOrdered(before, _windows, _popup)) return Result(null, "already-ordered", before);
            if (anchor is null) return Result("own-input-anchor-missing", "preflight", before);

            _popupPlacementAnchor = anchor.Value;
            bool popupPlaced = _native.PlacePopupAfterOwnWindow(_popup, anchor.Value, _owner);
            int? popupError = _native.LastPlacementError;
            string? popupFailure = _native.LastPlacementFailure;
            _popupPlacementAnchor = 0;
            var middle = _native.ReadZOrder().ToArray();
            popupPlacement = new { placed = popupPlaced, nativeError = popupError, nativeFailure = popupFailure,
                outsidePopupOrderUnchanged = SameOutsidePopup(before, middle, _popup),
                immediatelyAfterInput = ImmediatelyAfter(middle, _popup, anchor.Value) };
            if (!popupPlaced || !SameOutsidePopup(before, middle, _popup) ||
                !ImmediatelyAfter(middle, _popup, anchor.Value))
                return Result(!popupPlaced ? "placement-failed" : "order-mismatch", "popup-after-input", middle,
                    popupPlaced, popupError, popupFailure);

            // Only this exact input/Popup pair may cross while all other participants stay frozen.
            _permittedInput = anchor.Value;
            bool inputPlaced;
            try { inputPlaced = _native.PlaceInputAfterPopup(anchor.Value, _popup, _owner); }
            finally { _permittedInput = 0; }
            int? inputError = _native.LastPlacementError;
            string? inputFailure = _native.LastPlacementFailure;
            var after = _native.ReadZOrder().ToArray();
            return Result(!inputPlaced ? "placement-failed" :
                !SameOutsidePopup(before, after, _popup) || !IsOrdered(after, _windows, _popup) ? "order-mismatch" : null,
                "input-after-popup", after, inputPlaced, inputError, inputFailure);
        }
        finally { _permittedInput = 0; _popupPlacementAnchor = 0; _placing = false; }
    }

    private static bool SameOutsidePopup(IReadOnlyList<DesktopMenuZWindow> before,
        IReadOnlyList<DesktopMenuZWindow> after, nint popup) =>
        before.Where(w => w.Handle != popup).SequenceEqual(after.Where(w => w.Handle != popup));

    private static bool ImmediatelyAfter(IReadOnlyList<DesktopMenuZWindow> order, nint window, nint anchor)
    {
        for (int i = 1; i < order.Count; i++)
            if (order[i].Handle == window) return !order[i].Topmost && order[i - 1].Handle == anchor;
        return false;
    }

    private object CaptureOrderEvidence(IReadOnlyList<DesktopMenuZWindow> order, nint? anchor)
    {
        var focused = new HashSet<nint>(_windows) { _owner, _popup };
        if (anchor is { } a && a != 0) focused.Add(a);
        var indices = new SortedSet<int>();
        for (int i = 0; i < order.Count; i++)
            if (focused.Contains(order[i].Handle))
                for (int j = Math.Max(0, i - 2); j <= Math.Min(order.Count - 1, i + 2); j++) indices.Add(j);
        int Index(nint window)
        {
            for (int i = 0; i < order.Count; i++) if (order[i].Handle == window) return i;
            return -1;
        }
        return new
        {
            count = order.Count, popupIndex = Index(_popup),
            firstSurfaceIndex = Enumerable.Range(0, order.Count).FirstOrDefault(i => _windows.Contains(order[i].Handle), -1),
            // Missing focus windows are retained here even when absent from visible Z order.
            focus = focused.OrderBy(w => w.ToInt64()).Select(w => new
            { index = Index(w), window = _native.DescribeWindow(w) }).ToArray(),
            neighbors = indices.Select(i => new
            {
                index = i, handle = $"0x{order[i].Handle:X}", order[i].Topmost,
                popup = order[i].Handle == _popup, owner = order[i].Handle == _owner,
                surface = _windows.Contains(order[i].Handle), anchor = order[i].Handle == anchor
            }).ToArray()
        };
    }

    private void BeforePopupMessage(uint message, nint lParam)
    {
        if (_disposed || _closing) return;
        try
        {
            if (message == 0x0082) { _enqueue(Cancel); return; } // WM_NCDESTROY
            if (message == 0x0046 && lParam != 0) // WM_WINDOWPOSCHANGING
            {
                var position = Marshal.PtrToStructure<MenuWindowPos>(lParam);
                if ((position.Flags & 0x0080) != 0) return; // allow hide/close
                if (_placing && _popupPlacementAnchor != 0)
                { position.InsertAfter = _popupPlacementAnchor; position.Flags &= ~0x0004u; }
                else position.Flags |= 0x0004; // External requests preserve the proven menu position.
                position.Flags |= 0x0010 | 0x0200;
                Marshal.StructureToPtr(position, lParam, false);
            }
            else if (message == 0x0047 && !_placing && // Do not fight external ordering with retries.
                     !IsOrdered(_native.ReadZOrder(), _windows, _popup))
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
