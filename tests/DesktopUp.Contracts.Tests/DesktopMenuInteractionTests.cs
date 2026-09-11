using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class DesktopMenuInteractionTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void AutoPosture_menu_activity_is_paired_even_after_prepare_failure_and_faulty_observer(bool fail)
    {
        var f = new Fixture(3); var states = new List<bool>();
        void Broken() => throw new InvalidOperationException("observer fault");
        void Observe() => states.Add(DesktopMenuInteraction.IsActive);
        DesktopMenuInteraction.ActivityChanged += Broken;
        DesktopMenuInteraction.ActivityChanged += Observe;
        try
        {
            if (fail) { f.Native.FailTransparency = 22; Assert.Throws<InvalidOperationException>(() => f.Open()); }
            else { using var menu = f.Open(); Assert.True(menu.ActivatePopup()); }
            Assert.Equal(new[] { true, false }, states);
            Assert.False(DesktopMenuInteraction.IsActive); Assert.Empty(f.Ledger.ActiveResources);
        }
        finally
        {
            DesktopMenuInteraction.ActivityChanged -= Broken;
            DesktopMenuInteraction.ActivityChanged -= Observe;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void MenuReleasesAllIslandsAndRestoresExactInputBits(int count)
    {
        var f = new Fixture(count);
        f.Native.Transparent[12] = true; // do not erase a pre-existing transparency bit
        using (var menu = f.Open())
        {
            Assert.True(DesktopMenuInteraction.IsActive);
            Assert.Equal(count, f.Cancelled);
            Assert.Equal(count, f.Native.Transparent.Count(p => p.Value));
            Assert.All(f.Surfaces, s => Assert.True(DesktopMenuInteraction.BlocksInput(s.Input)));
            Assert.True(menu.ActivatePopup());
            Assert.True(menu.ActivatePopup());
            Assert.Equal(1, f.Native.Hooks);
            Assert.Equal((nint)100, f.Native.LastAnchor); // ordinary application stays above the menu
            Assert.True(DesktopMenuInteraction.IsOrdered(f.Native.Order, f.Handles, 200));
            Assert.Single(f.Ledger.ActiveResources);
        }
        Assert.Equal(count, f.Restored);
        Assert.False(DesktopMenuInteraction.IsActive);
        Assert.True(f.Native.Transparent[12]);
        Assert.Single(f.Native.Transparent.Where(p => p.Value));
        Assert.All(f.Surfaces, s => Assert.False(DesktopMenuInteraction.BlocksInput(s.Input)));
        Assert.Empty(f.Ledger.ActiveResources);
        Assert.Equal(0, f.Native.Hooks);
    }

    [Fact]
    public void FailedPrepareRollsBackEveryPreviouslyChangedIsland()
    {
        var f = new Fixture(3);
        f.Native.FailTransparency = 22;
        Assert.Throws<InvalidOperationException>(() => f.Open());
        Assert.All(f.Native.Transparent, p => Assert.False(p.Value));
        Assert.Empty(f.Ledger.ActiveResources);
        Assert.False(DesktopMenuInteraction.ProtectsSurface(11));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("hook")]
    [InlineData("placement")]
    [InlineData("readback")]
    public void FailedPopupActivationClosesAndReleasesProtection(string fault)
    {
        var f = new Fixture(3);
        f.Native.Fault = fault;
        using var menu = f.Open();
        Assert.False(menu.ActivatePopup());
        Assert.Equal(1, f.Closed);
        Assert.Empty(f.Ledger.ActiveResources);
        Assert.All(f.Native.Transparent, p => Assert.False(p.Value));
        Assert.Equal(0, f.Native.Hooks);
    }

    [Fact]
    public void SessionReplacementDoesNotLetOldCloseReleaseNewSession()
    {
        var f = new Fixture(3);
        using var old = f.Open();
        using var current = f.Open();
        Assert.Equal(1, f.Closed);
        old.Dispose();
        Assert.True(DesktopMenuInteraction.BlocksInput(22));
        Assert.Single(f.Ledger.ActiveResources);
        DesktopMenuInteraction.CancelForSurface(31); // neighbor removed/reprojected
        Assert.Equal(2, f.Closed);
        Assert.Empty(f.Ledger.ActiveResources);
    }

    [Fact]
    public void UnrelatedSurfaceDoesNotCancelMenuAndDestructionIsIdempotent()
    {
        var f = new Fixture(3);
        using var menu = f.Open();
        DesktopMenuInteraction.CancelForSurface(999);
        Assert.Single(f.Ledger.ActiveResources);
        f.Native.Alive.Remove(32);
        menu.Cancel(); menu.Cancel(); menu.Dispose();
        Assert.Equal(1, f.Closed);
        Assert.Empty(f.Ledger.ActiveResources);
        Assert.Equal(2, f.Restored);
    }

    [Fact]
    public void TopmostWindowIsNeverUsedAsInsertionAnchor()
    {
        DesktopMenuZWindow[] order = [new(99, true), new(11, false), new(12, false), new(200, false)];
        Assert.Equal((nint)0, DesktopMenuInteraction.ResolveAnchor(order, new HashSet<nint> { 11, 12 }, 200));
        Assert.Null(DesktopMenuInteraction.ResolveAnchor(order, new HashSet<nint> { 300 }, 200));
        Assert.False(DesktopMenuInteraction.IsOrdered([new(200, true), new(11, false)], new HashSet<nint> { 11 }, 200));
    }

    [Fact]
    public void SurfaceFreezePreservesGeometryAndDoesNotUseBottomOrTopmost()
    {
        var f = new Fixture(3);
        using var menu = f.Open();
        WithPosition(new() { Window = 11, InsertAfter = -1, X = -900, Y = 30, Width = 400, Height = 500 }, memory =>
        {
            Assert.True(DesktopMenuInteraction.PreserveSurfaceZOrder(11, memory));
            var position = Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory);
            Assert.Equal(-900, position.X); Assert.Equal(400, position.Width);
            Assert.Equal(0x214u, position.Flags & 0x214u);
            Assert.False(DesktopMenuInteraction.PreserveSurfaceZOrder(999, memory));
        });
    }

    [Fact]
    public void PopupGuardRecomputesAnchorAndNeverMovesItsOwner()
    {
        var f = new Fixture(3);
        using var menu = f.Open();
        Assert.True(menu.ActivatePopup());
        f.Native.Order.Insert(1, new(101, false));
        WithPosition(new() { Window = 200, InsertAfter = -1, Flags = 4, X = -40, Width = 180 }, memory =>
        {
            f.Native.Before!(0x0046, memory);
            var p = Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory);
            Assert.Equal((nint)101, p.InsertAfter);
            Assert.Equal(0x210u, p.Flags & 0x214u);
            Assert.Equal(-40, p.X); Assert.Equal(180, p.Width);
        });
        f.Native.Before!(0x0082, 0);
        Assert.Equal(0, f.Closed); // never destroy inside a native message
        f.Queue.Dequeue()();
        Assert.Equal(1, f.Closed);
    }

    private static void WithPosition(DesktopMenuInteraction.MenuWindowPos position, Action<nint> check)
    {
        nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<DesktopMenuInteraction.MenuWindowPos>());
        try { Marshal.StructureToPtr(position, memory, false); check(memory); }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private sealed class Fixture
    {
        public FakeNative Native = new();
        public FakeLedger Ledger = new();
        public Queue<Action> Queue = new();
        public DesktopMenuSurface[] Surfaces;
        public HashSet<nint> Handles;
        public int Cancelled, Restored, Closed;
        public Fixture(int count)
        {
            Surfaces = Enumerable.Range(1, count).Select(i => new DesktopMenuSurface(i * 10 + 1, i * 10 + 2,
                () => Cancelled++, () => { Restored++; return true; })).ToArray();
            Handles = Surfaces.SelectMany(s => new[] { s.Visual, s.Input }).ToHashSet();
            Native.Alive.UnionWith(Handles); Native.Alive.Add(200);
            Native.Order.Add(new(100, false));
            foreach (var s in Surfaces.Reverse())
            {
                Native.Order.Add(new(s.Input, false)); Native.Order.Add(new(s.Visual, false));
                Native.Transparent[s.Input] = false;
            }
            Native.Order.Add(new(200, false)); // replay: menu starts below input AND neighbor visual
        }
        public DesktopMenuInteraction Open() => DesktopMenuInteraction.Begin(11, Surfaces,
            () => Closed++, action => Queue.Enqueue(action), Ledger, new FakeLog(), Native);
    }

    private sealed class FakeNative : IDesktopMenuNative
    {
        public HashSet<nint> Alive = [];
        public Dictionary<nint, bool> Transparent = [];
        public List<DesktopMenuZWindow> Order = [];
        public nint FailTransparency, LastAnchor;
        public string? Fault;
        public int Hooks;
        public Action<uint, nint>? Before;
        public bool Exists(nint window) => Alive.Contains(window);
        public bool IsInputTransparent(nint window) => Transparent[window];
        public bool SetInputTransparent(nint window, bool value)
        {
            if (value && window == FailTransparency) return false;
            Transparent[window] = value; return true;
        }
        public nint FindPopup(nint owner) => Fault == "missing" ? 0 : 200;
        public IReadOnlyList<DesktopMenuHit> SamplePopup(nint popup) => [];
        public IReadOnlyList<DesktopMenuZWindow> ReadZOrder() => Order;
        public bool PlacePopup(nint popup, nint anchor)
        {
            if (Fault == "placement") return false;
            LastAnchor = anchor;
            if (Fault == "readback") return true;
            Order.RemoveAll(w => w.Handle == popup);
            Order.Insert(anchor == 0 ? 0 : Order.FindIndex(w => w.Handle == anchor) + 1, new(popup, false));
            return true;
        }
        public IDisposable WatchPopup(nint popup, Action<uint, nint> callback)
        {
            if (Fault == "hook") throw new InvalidOperationException("hook-fault");
            Before = callback; Hooks++;
            return new Lease(() => { Hooks--; Before = null; });
        }
    }
    private sealed class FakeLog : IStructuredLog { public void Write(string level, string eventName, object? data = null) { } }
    private sealed class FakeLedger : IResourceLedger
    {
        private readonly HashSet<string> _resources = [];
        public IReadOnlyCollection<string> ActiveResources => _resources;
        public IDisposable Acquire(string id, string kind)
        {
            if (!_resources.Add(id)) throw new InvalidOperationException("duplicate lease");
            return new Lease(() => _resources.Remove(id));
        }
        public void WriteSnapshot() { }
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; release(); } }
    }
}
