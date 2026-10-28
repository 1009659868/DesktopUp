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
            Assert.Equal(new[] { ((nint)200, f.Surfaces[^1].Input), (f.Surfaces[^1].Input, (nint)200) }, f.Native.Placements);
            Assert.Equal((nint)100, f.Native.Order[0].Handle); // ordinary application stays above the menu
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
    public void OnlyTheFirstPairedInputCanBeAnAnchorAndForeignWindowsAreNeverCandidates()
    {
        var surfaces = new HashSet<nint> { 11, 12 }; var inputs = new HashSet<nint> { 12 };
        DesktopMenuZWindow[] order = [new(99, true), new(100, false), new(12, false), new(11, false), new(200, false)];
        Assert.Equal((nint)12, DesktopMenuInteraction.ResolveOwnInputAnchor(order, surfaces, inputs, 200));
        Assert.Null(DesktopMenuInteraction.ResolveOwnInputAnchor([new(11, false), new(12, false)], surfaces, inputs, 200));
        Assert.Null(DesktopMenuInteraction.ResolveOwnInputAnchor([new(12, true), new(11, false)], surfaces, inputs, 200));
        Assert.Null(DesktopMenuInteraction.ResolveOwnInputAnchor(order, new HashSet<nint> { 300 }, inputs, 200));
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
    public void ExternalPopupReorderIsFrozenAndInvalidOrderClosesWithoutRepair()
    {
        var f = new Fixture(3);
        using var menu = f.Open();
        Assert.True(menu.ActivatePopup());
        WithPosition(new() { Window = 200, InsertAfter = -1, Flags = 4, X = -40, Width = 180 }, memory =>
        {
            f.Native.Before!(0x0046, memory);
            var p = Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory);
            Assert.Equal((nint)(-1), p.InsertAfter); // ignored because NOZORDER is retained
            Assert.Equal(0x214u, p.Flags & 0x214u);
            Assert.Equal(-40, p.X); Assert.Equal(180, p.Width);
        });
        int first = f.Native.Order.FindIndex(w => f.Handles.Contains(w.Handle));
        f.Native.Order.Insert(first, new(101, false)); // outsider interposes after the menu
        f.Native.Before!(0x0047, 0);
        Assert.Equal(0, f.Closed); // never destroy inside a native message
        Assert.Equal(2, f.Native.Placements.Count); // no retry can reorder the outsider
        f.Queue.Dequeue()();
        Assert.Equal(1, f.Closed);
    }

    [Theory]
    [InlineData("placement", 1)]
    [InlineData("input-placement", 2)]
    [InlineData("readback", 1)]
    [InlineData("input-readback", 2)]
    [InlineData("popup-exception", 1)]
    [InlineData("input-exception", 2)]
    public void EachFailedOrLyingNativeStepClosesRestoresAndNeverRetries(string fault, int attempts)
    {
        var f = new Fixture(3); f.Native.Fault = fault;
        f.Native.Transparent[12] = true;
        using var menu = f.Open();
        Assert.False(menu.ActivatePopup());
        Assert.Equal(attempts, f.Native.Placements.Count);
        Assert.All(f.Native.Placements, p => Assert.Contains(p.Anchor, f.Native.Alive));
        Assert.Equal(1, f.Closed); Assert.Equal(3, f.Restored);
        Assert.Empty(f.Ledger.ActiveResources); Assert.Equal(0, f.Native.Hooks);
        Assert.True(f.Native.Transparent[12]); Assert.Single(f.Native.Transparent.Where(p => p.Value));
        Assert.False(DesktopMenuInteraction.IsActive);
        WithPosition(new() { Window = 32, InsertAfter = 200, Flags = 3 }, p =>
            Assert.False(DesktopMenuInteraction.PreserveSurfaceZOrder(32, p)));
    }

    [Theory]
    [InlineData("popup-topmost")]
    [InlineData("input-topmost")]
    [InlineData("visual-first")]
    [InlineData("wrong-owner")]
    [InlineData("foreign-surface")]
    [InlineData("missing-surface")]
    public void UnprovenOrTopmostGroupsAreRejectedBeforeAnyPlacement(string fault)
    {
        var f = new Fixture(3);
        if (fault == "popup-topmost") f.Native.Order[^1] = new(200, true);
        if (fault == "input-topmost") f.Native.Order[1] = new(32, true);
        if (fault == "visual-first") (f.Native.Order[1], f.Native.Order[2]) = (f.Native.Order[2], f.Native.Order[1]);
        if (fault == "missing-surface") f.Native.Order.RemoveAll(w => w.Handle == 22);
        if (fault is "wrong-owner" or "foreign-surface") f.Native.Fault = fault;
        using var menu = f.Open();
        Assert.False(menu.ActivatePopup());
        Assert.Empty(f.Native.Placements); Assert.Equal(1, f.Closed); Assert.Empty(f.Ledger.ActiveResources);
        if (fault == "popup-topmost") Assert.True(f.Native.Order.Single(w => w.Handle == 200).Topmost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutsideInsertionOrSurfaceReorderDuringEitherStepFailsTheFullOrderWitness(bool secondStep)
    {
        foreach (bool reorderSurface in new[] { false, true })
        {
            var f = new Fixture(3);
            void ChangeOrder()
            {
                if (reorderSurface)
                {
                    int a = f.Native.Order.FindIndex(w => w.Handle == 21), b = f.Native.Order.FindIndex(w => w.Handle == 11);
                    (f.Native.Order[a], f.Native.Order[b]) = (f.Native.Order[b], f.Native.Order[a]);
                }
                else f.Native.Order.Insert(0, new(101, false));
            }
            if (secondStep) f.Native.AfterInputPlacement = ChangeOrder; else f.Native.AfterPopupPlacement = ChangeOrder;
            using var menu = f.Open();
            Assert.False(menu.ActivatePopup());
            Assert.Equal(secondStep ? 2 : 1, f.Native.Placements.Count);
            Assert.Equal(1, f.Closed); Assert.Equal(3, f.Restored); Assert.Empty(f.Ledger.ActiveResources);
        }
    }

    [Fact]
    public void SecondStepPermissionRequiresExactInputPopupAndNoGeometryFlags()
    {
        var f = new Fixture(3); int checks = 0;
        f.Native.BeforeInputPlacement = () =>
        {
            foreach (var (window, after, flags, allowed) in new (nint, nint, uint, bool)[]
            {
                (32, 200, 3, true), (31, 200, 3, false), (22, 200, 3, false),
                (32, 100, 3, false), (32, 200, 1, false), (32, 200, 2, false),
                (32, 200, 0x43, false), (32, 200, 0x83, false), (32, 200, 7, false)
            })
                WithPosition(new() { Window = window, InsertAfter = after, Flags = flags, X = -900, Width = 400 }, memory =>
                {
                    Assert.True(DesktopMenuInteraction.PreserveSurfaceZOrder(window, memory));
                    var p = Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory);
                    Assert.Equal(allowed, (p.Flags & 4) == 0); Assert.Equal(0x210u, p.Flags & 0x210u);
                    Assert.Equal(-900, p.X); Assert.Equal(400, p.Width); checks++;
                });
        };
        using var menu = f.Open();
        Assert.True(menu.ActivatePopup()); Assert.Equal(9, checks);
        WithPosition(new() { Window = 32, InsertAfter = 200, Flags = 3 }, memory =>
        {
            Assert.True(DesktopMenuInteraction.PreserveSurfaceZOrder(32, memory));
            Assert.NotEqual(0u, Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory).Flags & 4);
        });
    }

    [Fact]
    public void OwnAnchorExchangePreservesEveryOtherWindowAndRepeatedOpenedDoesNoMoreNativeWork()
    {
        var f = new Fixture(3);
        f.Native.Order.RemoveAll(w => w.Handle == 200); f.Native.Order.Insert(0, new(200, false));
        var before = f.Native.Order.Where(w => w.Handle != 200).ToArray();
        using var menu = f.Open();
        Assert.True(menu.ActivatePopup());
        Assert.Equal(before, f.Native.Order.Where(w => w.Handle != 200).ToArray());
        Assert.True(DesktopMenuInteraction.IsOrdered(f.Native.Order, f.Handles, 200));
        Assert.Equal(new[] { ((nint)200, (nint)32), ((nint)32, (nint)200) }, f.Native.Placements);
        Assert.True(menu.ActivatePopup()); Assert.Equal(2, f.Native.Placements.Count);
        Assert.Empty(f.Queue); // Own synchronous WINDOWPOSCHANGED callbacks must not schedule repair.
    }

    [Fact]
    public void RepeatedOpenedWithInvalidOwnerClosesAndReleasesInsteadOfLeavingAnUnprovenMenu()
    {
        var f = new Fixture(3);
        using var menu = f.Open();
        Assert.True(menu.ActivatePopup());
        f.Native.Fault = "wrong-owner";
        Assert.False(menu.ActivatePopup());
        Assert.Equal(2, f.Native.Placements.Count); Assert.Equal(1, f.Closed);
        Assert.Equal(3, f.Restored); Assert.Empty(f.Ledger.ActiveResources);
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
        public List<(nint Window, nint Anchor)> Placements = [];
        public Action? AfterPopupPlacement, BeforeInputPlacement, AfterInputPlacement;
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
        public bool IsOwnUiWindow(nint window) => Alive.Contains(window) && !(Fault == "foreign-surface" && window == 22);
        public bool IsOwnedPopup(nint popup, nint owner) => Fault != "wrong-owner" && popup == 200 && IsOwnUiWindow(owner);
        public bool PlacePopupAfterOwnWindow(nint popup, nint anchor, nint owner)
        {
            Placements.Add((popup, anchor));
            if (Fault == "popup-exception") throw new InvalidOperationException("popup native exception");
            if (Fault == "placement") return false;
            LastAnchor = anchor;
            if (Fault == "readback") return true;
            WithPosition(new() { Window = popup, InsertAfter = anchor, Flags = 0x213 }, memory =>
            {
                Before!(0x0046, memory);
                var p = Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory);
                Assert.Equal(anchor, p.InsertAfter); Assert.Equal(0u, p.Flags & 4);
                Move(popup, anchor);
            });
            AfterPopupPlacement?.Invoke(); Before!(0x0047, 0);
            return true;
        }
        public bool PlaceInputAfterPopup(nint input, nint popup, nint owner)
        {
            Placements.Add((input, popup));
            if (Fault == "input-exception") throw new InvalidOperationException("input native exception");
            if (Fault == "input-placement") return false;
            if (Fault == "input-readback") return true;
            BeforeInputPlacement?.Invoke();
            WithPosition(new() { Window = input, InsertAfter = popup, Flags = 0x213 }, memory =>
            {
                Assert.True(DesktopMenuInteraction.PreserveSurfaceZOrder(input, memory));
                var p = Marshal.PtrToStructure<DesktopMenuInteraction.MenuWindowPos>(memory);
                Assert.Equal(0u, p.Flags & 4); Assert.Equal(popup, p.InsertAfter);
                Move(input, popup);
            });
            AfterInputPlacement?.Invoke(); Before!(0x0047, 0);
            return true;
        }
        private void Move(nint window, nint after)
        {
            var original = Order.Single(w => w.Handle == window); // Never invent non-topmost state.
            Order.RemoveAll(w => w.Handle == window);
            Order.Insert(Order.FindIndex(w => w.Handle == after) + 1, original);
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
