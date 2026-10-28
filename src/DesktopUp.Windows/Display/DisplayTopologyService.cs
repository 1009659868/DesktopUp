using System.Globalization;
using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

public sealed class DisplayTopologyOptions
{
    public int RequiredStableSamples { get; init; } = 2;
    public int MaximumProbeAttempts { get; init; } = 16;
    public TimeSpan ProbeInterval { get; init; } = TimeSpan.FromMilliseconds(180);

    internal void Validate()
    {
        if (RequiredStableSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredStableSamples));
        }
        if (MaximumProbeAttempts < RequiredStableSamples)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumProbeAttempts));
        }
        if (ProbeInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ProbeInterval));
        }
    }
}

internal interface IDisplayTopologyApi
{
    bool TryCapture(
        out string signature,
        out IReadOnlyList<DisplayMonitorSnapshot> monitors);
}

internal sealed class DisplayTopologyStabilityTracker(int requiredStableSamples)
{
    private readonly int _requiredStableSamples = Math.Max(1, requiredStableSamples);
    private string? _candidateSignature;
    private IReadOnlyList<DisplayMonitorSnapshot> _candidateMonitors = [];
    private int _consecutiveSamples;
    private long _generation;

    public DisplayTopologySnapshot? Current { get; private set; }

    public DisplayTopologyProbeResult Observe(
        string? signature,
        IReadOnlyList<DisplayMonitorSnapshot>? monitors,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(signature) || monitors is null || monitors.Count == 0)
        {
            ResetCandidate();
            return new DisplayTopologyProbeResult(
                DisplayTopologyProbeState.Missing,
                Current,
                null,
                0);
        }

        if (string.Equals(Current?.Signature, signature, StringComparison.Ordinal))
        {
            ResetCandidate();
            return new DisplayTopologyProbeResult(
                DisplayTopologyProbeState.Unchanged,
                Current,
                signature,
                _requiredStableSamples);
        }

        if (!string.Equals(_candidateSignature, signature, StringComparison.Ordinal))
        {
            _candidateSignature = signature;
            _candidateMonitors = monitors.ToArray();
            _consecutiveSamples = 1;
        }
        else
        {
            _candidateMonitors = monitors.ToArray();
            _consecutiveSamples++;
        }

        if (_consecutiveSamples < _requiredStableSamples)
        {
            return new DisplayTopologyProbeResult(
                DisplayTopologyProbeState.Candidate,
                Current,
                signature,
                _consecutiveSamples);
        }

        Current = new DisplayTopologySnapshot(
            signature,
            _candidateMonitors,
            checked(++_generation),
            observedAtUtc);
        ResetCandidate();
        return new DisplayTopologyProbeResult(
            DisplayTopologyProbeState.Stable,
            Current,
            signature,
            _requiredStableSamples);
    }

    private void ResetCandidate()
    {
        _candidateSignature = null;
        _candidateMonitors = [];
        _consecutiveSamples = 0;
    }
}

public sealed class DisplayTopologyService : IDisplayTopologyService
{
    private readonly object _gate = new();
    private readonly IDisplayTopologyApi _api;
    private readonly DisplayTopologyOptions _options;
    private readonly DisplayTopologyStabilityTracker _tracker;

    public DisplayTopologyService()
        : this(new DisplayTopologyNativeApi(), new DisplayTopologyOptions())
    {
    }

    internal DisplayTopologyService(
        IDisplayTopologyApi api,
        DisplayTopologyOptions options)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _api = api;
        _options = options;
        _tracker = new DisplayTopologyStabilityTracker(options.RequiredStableSamples);
    }

    public DisplayTopologySnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                return _tracker.Current;
            }
        }
    }

    public DisplayTopologyProbeResult Probe()
    {
        lock (_gate)
        {
            if (!_api.TryCapture(
                    out string signature,
                    out IReadOnlyList<DisplayMonitorSnapshot> monitors))
            {
                return _tracker.Observe(null, null, DateTimeOffset.UtcNow);
            }

            return _tracker.Observe(signature, monitors, DateTimeOffset.UtcNow);
        }
    }

    public async ValueTask<DisplayTopologySnapshot?> WaitForStableTopologyAsync(
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= _options.MaximumProbeAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DisplayTopologyProbeResult result = Probe();
            if (result.State is DisplayTopologyProbeState.Stable or DisplayTopologyProbeState.Unchanged)
            {
                return result.Snapshot;
            }

            if (attempt < _options.MaximumProbeAttempts && _options.ProbeInterval > TimeSpan.Zero)
            {
                await Task.Delay(_options.ProbeInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    public bool IsCurrent(DisplayTopologySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            return _api.TryCapture(out string signature, out _) &&
                string.Equals(snapshot.Signature, signature, StringComparison.Ordinal);
        }
    }
}

internal sealed class DisplayTopologyNativeApi : IDisplayTopologyApi
{
    private const uint MonitorInfoPrimary = 0x00000001;
    private const int EffectiveDpi = 0;

    public bool TryCapture(
        out string signature,
        out IReadOnlyList<DisplayMonitorSnapshot> monitors)
    {
        var captured = new List<DisplayMonitorSnapshot>();
        bool enumerated = EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx
            {
                Size = checked((uint)Marshal.SizeOf<MonitorInfoEx>()),
                DeviceName = string.Empty
            };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            uint dpiX = 96;
            uint dpiY = 96;
            try
            {
                if (GetDpiForMonitor(
                        monitor,
                        EffectiveDpi,
                        out uint measuredX,
                        out uint measuredY) == 0)
                {
                    dpiX = measuredX;
                    dpiY = measuredY;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            captured.Add(new DisplayMonitorSnapshot(
                info.DeviceName ?? string.Empty,
                (info.Flags & MonitorInfoPrimary) != 0,
                ToRect(info.Monitor),
                ToRect(info.WorkArea),
                Math.Max(96, dpiX),
                Math.Max(96, dpiY)));
            return true;
        }, 0);

        captured = captured
            .OrderBy(static monitor => monitor.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static monitor => monitor.MonitorBounds.X)
            .ThenBy(static monitor => monitor.MonitorBounds.Y)
            .ToList();

        monitors = captured;
        if (!enumerated || captured.Count == 0)
        {
            signature = string.Empty;
            return false;
        }

        signature = string.Join(
            "|",
            captured.Select(static monitor => string.Format(
                CultureInfo.InvariantCulture,
                "{0};{1};{2},{3};{4},{5},{6},{7};{8},{9},{10},{11}",
                monitor.DeviceName,
                monitor.IsPrimary,
                monitor.DpiX,
                monitor.DpiY,
                monitor.MonitorBounds.X,
                monitor.MonitorBounds.Y,
                monitor.MonitorBounds.Width,
                monitor.MonitorBounds.Height,
                monitor.WorkArea.X,
                monitor.WorkArea.Y,
                monitor.WorkArea.Width,
                monitor.WorkArea.Height)));
        return true;
    }

    private static DesktopPixelRect ToRect(NativeRect rect) => new(
        rect.Left,
        rect.Top,
        Math.Max(1, rect.Right - rect.Left),
        Math.Max(1, rect.Bottom - rect.Top));

    private delegate bool MonitorEnumProc(
        nint monitor,
        nint deviceContext,
        nint monitorRect,
        nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRect,
        MonitorEnumProc callback,
        nint data);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetMonitorInfoW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        nint monitor,
        ref MonitorInfoEx monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
