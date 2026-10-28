using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PassivePosterPointerTests
{
    private static readonly PosterInputSessionKey A = new("A", 1);
    private static readonly DesktopPixelRect Bounds = new(-300, 100, 300, 360);
    private static PosterInputLayoutSnapshot Layout(long revision = 1, int width = 300, int height = 360, int radius = 8) =>
        new(revision, width, height, Array.AsReadOnly(new[]
        {
            new PosterInputRegion(PosterInputTargetKind.CollapseButton, -1, new(width - 80, 0, 40, 48)),
            new PosterInputRegion(PosterInputTargetKind.MoreButton, -1, new(width - 40, 0, 40, 48)),
            new PosterInputRegion(PosterInputTargetKind.Item, 0, new(20, 70, 90, 70)),
            new PosterInputRegion(PosterInputTargetKind.TitleBar, -1, new(0, 0, width, 48))
        }), radius);

    private sealed class Native : IDesktopPointerNative
    {
        public PointerScreenPoint Point = new(-250, 124);
        public nint Receiver = 11;
        public List<nint>? Order = [99, 12, 11, 90];
        public HashSet<nint> Cover = [];
        public bool CursorOk = true, ProtectionOk = true, SurfaceOk = true, DesktopOk = true;
        public AutoPostureProtection Protection;
        public int CursorReads, OrderReads;
        public bool TryReadCursor(out PointerScreenPoint point) { CursorReads++; point = Point; return CursorOk; }
        public bool TryReadProtection(out AutoPostureProtection protection) { protection = Protection; return ProtectionOk; }
        public nint ReceiverRoot(PointerScreenPoint point) => Receiver;
        public IReadOnlyList<nint>? ReadWindowOrder() { OrderReads++; return Order; }
        public bool IsSurfaceCurrent(nint visual, nint input, DesktopPixelRect frame) => SurfaceOk;
        public bool IsDesktopReceiver(nint root, DesktopShellHostSnapshot shell) => DesktopOk && root == shell.Identity.ContainerHandle;
        public bool IsDesktopRoot(nint root) => root is 90 or 95;
        public bool CoversPoint(nint window, PointerScreenPoint point) => Cover.Contains(window);
    }
    private sealed class Fixture
    {
        public readonly PosterInputRouter Router = new();
        public readonly Native Native = new();
        public readonly PassivePosterPointerObserver Observer;
        public List<PosterPointerTarget> Targets = [new(A, 11, 12, new(new(91, 90, 5), 1, DateTimeOffset.UnixEpoch),
            true, true, true, true)];
        private long _sequence;
        public Fixture()
        {
            Router.Register(A);
            Assert.True(Router.Publish(A, Bounds, Layout()));
            Observer = new(Router, Native);
        }
        public PosterPointerSnapshot Sample(AutoPostureProtection shared = AutoPostureProtection.None, long epoch = 1) =>
            Observer.Sample(++_sequence, epoch, Targets, shared);
        public AutoPosturePointer Pointer() => Sample().Posters.Single(p => p.Session.PosterId == "A").Pointer;
    }

    [Theory]
    [InlineData(-250, 124, AutoPosturePresence.ExpansionZone)]
    [InlineData(-70, 124, AutoPosturePresence.Inside)]
    [InlineData(-20, 124, AutoPosturePresence.Inside)]
    [InlineData(-250, 300, AutoPosturePresence.Inside)]
    [InlineData(0, 124, AutoPosturePresence.Outside)]
    [InlineData(-300, 100, AutoPosturePresence.Outside)]
    [InlineData(-250, 460, AutoPosturePresence.Outside)]
    [InlineData(-250, 500, AutoPosturePresence.Outside)]
    public void Published_logical_bounds_buttons_rounding_and_capacity(int x, int y, AutoPosturePresence expected)
    {
        var f = new Fixture(); f.Native.Point = new(x, y);
        Assert.Equal(expected, f.Pointer().Presence);
    }

    [Fact]
    public void Blank_is_observable_but_still_not_an_input_region()
    {
        var f = new Fixture(); f.Native.Point = new(-250, 300); f.Native.Receiver = 90;
        Assert.Null(f.Router.Resolve(-250, 300, [A]));
        Assert.Equal(AutoPosturePresence.Inside, f.Pointer().Presence);
        Assert.Null(f.Router.Capture);
        Assert.Equal(Bounds, f.Router.ReadSurface(A)!.Frame);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(90)]
    public void Visual_input_and_verified_desktop_ownership_are_accepted(int receiver)
    {
        var f = new Fixture(); f.Native.Receiver = receiver;
        Assert.Equal(AutoPosturePresence.ExpansionZone, f.Pointer().Presence);
    }

    [Theory]
    [InlineData(98)] // ordinary app
    [InlineData(99)] // Explorer folder (same process is insufficient)
    [InlineData(200)] // menu root, never ROOTOWNER
    public void Other_roots_cannot_inherit_desktop_or_poster_ownership(int receiver)
    {
        var f = new Fixture(); f.Native.Receiver = receiver;
        Assert.Equal(AutoPosturePresence.Outside, f.Pointer().Presence);
    }

    [Fact]
    public void Visible_disabled_occluder_blocks_even_if_native_point_lookup_skips_it()
    {
        var f = new Fixture(); f.Native.Cover.Add(99);
        Assert.Equal(AutoPosturePresence.Outside, f.Pointer().Presence);
        f.Native.Order = [12, 11, 99, 90];
        Assert.Equal(AutoPosturePresence.ExpansionZone, f.Pointer().Presence); // behind poster does not block
    }

    [Theory]
    [InlineData("cursor")]
    [InlineData("protection")]
    [InlineData("receiver")]
    [InlineData("order")]
    [InlineData("missing-pair")]
    [InlineData("surface")]
    [InlineData("suspended")]
    [InlineData("invalidated")]
    public void Unreliable_native_or_router_state_is_unknown(string fault)
    {
        var f = new Fixture();
        switch (fault)
        {
            case "cursor": f.Native.CursorOk = false; break;
            case "protection": f.Native.ProtectionOk = false; break;
            case "receiver": f.Native.Receiver = 0; break;
            case "order": f.Native.Order = null; break;
            case "missing-pair": f.Native.Order = [99, 90]; break;
            case "surface": f.Native.SurfaceOk = false; break;
            case "suspended": f.Router.Suspend(); break;
            case "invalidated": f.Router.Invalidate(A); break;
        }
        Assert.Equal(AutoPosturePresence.Unknown, f.Pointer().Presence);
    }

    [Fact]
    public void Alternate_unverified_desktop_root_is_unknown_not_a_false_leave()
    {
        var f = new Fixture(); f.Pointer(); f.Native.Receiver = 95;
        var pointer = f.Pointer();
        Assert.Equal(AutoPosturePresence.Unknown, pointer.Presence);
        Assert.Equal(0, pointer.ExitVersion);
    }

    [Fact]
    public void Stale_desktop_identity_is_not_an_allow_list()
    {
        var f = new Fixture(); f.Native.Receiver = 90; f.Native.DesktopOk = false;
        Assert.Equal(AutoPosturePresence.Unknown, f.Pointer().Presence);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void One_native_sample_for_complete_multi_poster_set(int count)
    {
        var f = new Fixture();
        for (int i = 1; i < count; i++)
        {
            var key = new PosterInputSessionKey($"B{i}", i + 1);
            f.Router.Register(key);
            Assert.True(f.Router.Publish(key, Bounds with { X = i * 300 }, Layout()));
            f.Targets.Add(f.Targets[0] with { Session = key, Visual = 20 + i, Input = 30 + i });
        }
        Assert.Equal(count, f.Sample().Posters.Count);
        Assert.Equal(1, f.Native.CursorReads); Assert.Equal(1, f.Native.OrderReads);
    }

    [Fact]
    public void Adjacent_posters_have_half_open_edges_and_action_exclusions()
    {
        var f = new Fixture(); var b = new PosterInputSessionKey("B", 2);
        f.Router.Register(b); Assert.True(f.Router.Publish(b, Bounds with { X = 0 }, Layout()));
        f.Targets.Add(f.Targets[0] with { Session = b, Visual = 21, Input = 22 });
        f.Native.Order = [22, 21, 12, 11, 90]; f.Native.Receiver = 22;
        f.Native.Point = new(0, 124);
        var observed = f.Sample().Posters;
        Assert.Equal(AutoPosturePresence.Outside, observed[0].Pointer.Presence);
        Assert.Equal(AutoPosturePresence.ExpansionZone, observed[1].Pointer.Presence);
        Assert.Null(f.Router.Capture);
    }

    [Theory]
    [InlineData(AutoPostureProtection.Editing)]
    [InlineData(AutoPostureProtection.Menu)]
    [InlineData(AutoPostureProtection.Confirmation)]
    [InlineData(AutoPostureProtection.PointerButton)]
    [InlineData(AutoPostureProtection.PointerCapture)]
    [InlineData(AutoPostureProtection.DragDrop)]
    [InlineData(AutoPostureProtection.Gesture)]
    [InlineData(AutoPostureProtection.Recovery)]
    public void Local_and_shared_protections_survive_normalization(AutoPostureProtection protection)
    {
        var f = new Fixture(); f.Targets[0] = f.Targets[0] with { Protection = protection };
        f.Native.Protection = protection;
        var snapshot = f.Sample();
        Assert.Equal(protection, snapshot.Posters[0].Protection);
        Assert.Equal(protection, snapshot.SharedProtection);
    }

    [Fact]
    public void Capture_is_observed_not_cancelled_or_stolen()
    {
        var f = new Fixture(); var hit = f.Router.Resolve(-250, 124, [A]);
        var token = f.Router.Begin(hit!);
        Assert.NotNull(token);
        Assert.True(f.Sample().SharedProtection.HasFlag(AutoPostureProtection.PointerCapture));
        Assert.False(f.Sample().SharedProtection.HasFlag(AutoPostureProtection.Gesture));
        Assert.Same(token, f.Router.Capture);
        f.Router.End(token);
        Assert.Equal(AutoPostureProtection.None, f.Sample().SharedProtection);
    }

    [Fact]
    public void Zero_disabled_hidden_and_unready_demand_do_not_read_native_pointer()
    {
        var f = new Fixture();
        f.Targets[0] = f.Targets[0] with { Enabled = false }; f.Sample();
        f.Targets[0] = f.Targets[0] with { Enabled = true, Visible = false }; f.Sample();
        f.Targets[0] = f.Targets[0] with { Visible = true, Ready = false }; f.Sample();
        f.Targets.Clear(); Assert.Empty(f.Sample().Posters);
        Assert.Equal(0, f.Native.CursorReads); Assert.Equal(0, f.Native.OrderReads);
    }
}
