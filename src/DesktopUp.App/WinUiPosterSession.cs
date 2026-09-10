using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed class WinUiPosterSessionFactory(
    IDesktopLayerAttachment attachment, IStateCommandService commands, IResourceLedger ledger,
    IStructuredLog log, Action showControlCenter, Action<string, bool> editingChanged,
    Action<DesktopEnvironmentChangedEventArgs> environmentChanged, PosterSpatialRuntime spatial,
    PosterInputRouter inputRouter, Action<PosterRemovalRequest> requestRemoval) : IPosterSessionFactory
{
    public IPosterSession Create(string posterId, long epoch)
    {
        var scopedLedger = new PosterSessionLedger(ledger, posterId, epoch);
        var scopedLog = new PosterSessionLog(log, posterId, epoch);
        return new WinUiPosterSession(posterId,
            new PosterVisualWindow(attachment, commands, scopedLedger, scopedLog, spatial,
                inputRouter, new(posterId, epoch), requestRemoval, showControlCenter,
                editing => editingChanged(posterId, editing)), scopedLedger, scopedLog, environmentChanged);
    }
}

internal sealed class WinUiPosterSession(string posterId, PosterVisualWindow visual, IResourceLedger ledger,
    IStructuredLog log, Action<DesktopEnvironmentChangedEventArgs> environmentChanged) : IPosterSession
{
    private DesktopEnvironmentWatcher? _watcher;
    private IDisposable? _watcherLease;
    private PosterSessionEnvironment? _environment;
    private CancellationToken _lifetime;
    private bool _disposed;
    public string PosterId => posterId;
    public nint WindowHandle => visual.WindowHandle;
    public bool IsEditing => visual.IsEditing;
    public DesktopRecoveryGenerationPair? CurrentGenerationPair => visual.CurrentGenerationPair;

    public bool Start(PosterSurfaceProjection projection, PosterSessionEnvironment environment, CancellationToken lifetime)
    {
        if (_disposed || lifetime.IsCancellationRequested || projection.Poster.PosterId != PosterId) return false;
        _lifetime = lifetime;
        _environment = environment;
        if (!visual.Show(environment.Shell, environment.Displays, projection)) return false;
        visual.RegisterSpatialSurface();
        _watcher = new DesktopEnvironmentWatcher(WindowHandle, ReassertBottom,
            compositionOwnsBackground: () => visual.HasWindowBackdrop);
        _watcher.Changed += OnEnvironmentChanged;
        _watcher.PointerActivationGuarded += OnPointerGuarded;
        _watcher.LayerActivationGuarded += OnLayerGuarded;
        _watcher.ZOrderChangeConstrained += OnZOrderConstrained;
        _watcherLease = ledger.Acquire("watcher.desktop-environment", "native-window-subclass");
        return !_lifetime.IsCancellationRequested;
    }

    public bool ApplyProjection(StateProjectionChangedEventArgs change) =>
        !_disposed && !_lifetime.IsCancellationRequested && visual.ApplyProjection(change);
    public bool Recover(PosterSessionEnvironment environment, long request)
    {
        if (_disposed || _lifetime.IsCancellationRequested) return false;
        _environment = environment;
        bool recovered = visual.Recover(environment.Shell, environment.Displays, request);
        if (!recovered) visual.SuspendInput();
        return recovered;
    }
    public bool IsAttached(DesktopShellHostSnapshot shell) => !_disposed && visual.IsAttached(shell);
    public bool SetEditing(bool editing) => !_disposed && !_lifetime.IsCancellationRequested && visual.SetEditing(editing);
    private bool ReassertBottom() => !_disposed && !_lifetime.IsCancellationRequested &&
        _environment is not null && visual.ReassertDesktopBottom(_environment.Shell);
    private void OnEnvironmentChanged(object? sender, DesktopEnvironmentChangedEventArgs args)
    {
        if (!_disposed && !_lifetime.IsCancellationRequested) environmentChanged(args);
    }
    private void GuardResult(string reason, bool restored, string? error)
    {
        log.Write(restored ? "info" : "warning", "poster.desktop_bottom_guarded", new { reason, restored, error });
        if (!restored && !_disposed && !_lifetime.IsCancellationRequested)
            environmentChanged(new(DesktopEnvironmentChangeKind.DesktopShell | DesktopEnvironmentChangeKind.DisplayTopology,
                "session-guard-" + reason));
    }
    private void OnPointerGuarded(object? sender, DesktopPointerActivationGuardedEventArgs args) =>
        GuardResult("pointer", args.DesktopBottomRestored, args.Error);
    private void OnLayerGuarded(object? sender, DesktopLayerActivationGuardedEventArgs args) =>
        GuardResult(args.Reason, args.DesktopBottomRestored, args.Error);
    private void OnZOrderConstrained(object? sender, DesktopZOrderChangeConstrainedEventArgs args) =>
        log.Write("info", "poster.z_order_constrained", new { requestedInsertAfter = $"0x{args.RequestedInsertAfter:X}" });

    public void Dispose()
    {
        _disposed = true;
        _watcher?.Dispose();
        _watcher = null;
        _watcherLease?.Dispose();
        _watcherLease = null;
        visual.Dispose();
        _environment = null;
    }
}
