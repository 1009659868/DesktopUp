using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DesktopUp.Application;
using DesktopUp.NativeDragProbe;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class IncomingDropNativeTests
{
    private static readonly FileDropPoint Point = new() { X = -900, Y = 0 };
    private const string Path = @"Z:\not-a-real-source\one.txt";
    private static ProbeDataObject Data(string path = Path)
    {
        var data = new ProbeDataObject([1]);
        byte[] names = Encoding.Unicode.GetBytes(path + "\0\0"), bytes = new byte[20 + names.Length];
        BitConverter.GetBytes(20).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 16); names.CopyTo(bytes, 20);
        data.Put(15, bytes); return data; // Only an in-memory HGLOBAL; never handed to Explorer.
    }
    [Theory] [InlineData(0u, 2u)] [InlineData(8u, 0u)] [InlineData(32u, 0u)] [InlineData(2u, 0u)]
    public void RegisteredCallbackImplementationKeepsImageAndNeverReturnsSourceDeletePermission(uint keys, uint expected)
    {
        using var data = Data(); var images = new Images(); var tickets = new List<IncomingDropTicket>();
        var surface = IncomingDropSessionTests.Surface();
        using var session = new NativeIncomingDropSession(1, () => surface, tickets.Add, images, new Log());
        uint effect = 3; session.DragEnter(data, keys, Point, ref effect); Assert.Equal(expected, effect);
        effect = 3; session.DragOver(keys, Point, ref effect); Assert.Equal(expected, effect);
        effect = 3; session.Drop(data, keys, Point, ref effect); Assert.Equal(0u, effect);
        effect = 3; session.Drop(data, keys, Point, ref effect); Assert.Equal(0u, effect);
        Assert.Equal(expected == 2 ? 1 : 0, tickets.Count); Assert.False(session.IsHovering);
        Assert.Equal(new[] { "enter", "over", "drop" }, images.Events); Assert.Equal(0u, images.DropEffect);
    }
    [Theory] [InlineData("enter")] [InlineData("over")] [InlineData("drop")] [InlineData("leave")] [InlineData("dispose")]
    public void HelperFailuresDoNotCrossComBoundaryOrRetainOwner(string failure)
    {
        using var data = Data(); var images = new Images { Failure = failure }; var log = new Log(); int calls = 0;
        var surface = IncomingDropSessionTests.Surface();
        var session = new NativeIncomingDropSession(1, () => surface, _ => calls++, images, log);
        uint effect = 2; session.DragEnter(data, 0, Point, ref effect);
        effect = 2; session.DragOver(0, Point, ref effect);
        if (failure == "drop") { effect = 2; session.Drop(data, 0, Point, ref effect); }
        session.Dispose(); session.Dispose();
        Assert.False(session.IsHovering); Assert.Equal(0, calls); Assert.NotEmpty(log.Errors);
        Assert.Equal(1, images.Events.Count(x => x == "dispose"));
        effect = 2; session.DragEnter(data, 0, Point, ref effect); Assert.Equal(0u, effect);
    }
    [Fact]
    public void PayloadMutationAndSurfaceGenerationChangeCannotQueueFiles()
    {
        using var data = Data(); using var changed = Data(@"Z:\different.txt"); var images = new Images(); int calls = 0;
        var surface = IncomingDropSessionTests.Surface();
        using var session = new NativeIncomingDropSession(1, () => surface, _ => calls++, images, new Log());
        uint effect = 2; session.DragEnter(data, 0, Point, ref effect);
        effect = 2; session.Drop(changed, 0, Point, ref effect);
        effect = 2; session.DragEnter(data, 0, Point, ref effect);
        surface = surface with { EntityVersion = 99 };
        effect = 2; session.Drop(data, 0, Point, ref effect);
        Assert.Equal(0, calls); Assert.Equal(new[] { "enter", "drop", "enter", "drop" }, images.Events);
    }
    [Fact]
    public void ReentryPairsLeaveAndConsumerExceptionCannotPermitAnotherDrop()
    {
        using var data = Data(); var images = new Images(); int calls = 0; var log = new Log();
        var surface = IncomingDropSessionTests.Surface();
        using var session = new NativeIncomingDropSession(1, () => surface, _ => { calls++; throw new IOException("consumer"); }, images, log);
        uint effect = 2; session.DragEnter(data, 0, Point, ref effect);
        effect = 2; session.DragEnter(data, 0, Point, ref effect);
        effect = 2; session.Drop(data, 0, Point, ref effect);
        effect = 2; session.Drop(data, 0, Point, ref effect);
        Assert.Equal(1, calls); Assert.NotEmpty(log.Errors);
        Assert.Equal(new[] { "enter", "leave", "enter", "drop" }, images.Events);
    }
    [Theory] [InlineData(40)] [InlineData(400)]
    public void OrdinaryNativeRegionRemainsSparseWhileOlePolicyCoversCurrentFrame(int height)
    {
        var frame = new DesktopPixelRect(-1000, -300, 300, height);
        var layout = new PosterInputLayoutSnapshot(1, 300, height,
            [new(PosterInputTargetKind.TitleBar, -1, new(10, 5, 280, 30))], 8);
        nint region = PosterInputIslandManager.BuildWindowRegion(layout);
        try
        {
            Assert.NotEqual(0, region);
            Assert.False(PtInRegion(region, 0, 0)); Assert.False(PtInRegion(region, 150, height - 1));
            Assert.True(PtInRegion(region, 100,20));
            Assert.False(PtInRegion(region, 150, height));
            Assert.True(PosterDropSurfacePolicy.ReceivesOle(frame, new(-1000, -300)));
            Assert.False(PosterDropSurfacePolicy.ReceivesPointer(frame, layout, new(-1000, -300)));
            Assert.True(PosterDropSurfacePolicy.ReceivesPointer(frame, layout, new(-900, -280)));
            Assert.False(PosterDropSurfacePolicy.ReceivesPointer(frame, layout, new(-900, -300 + height - 1)));
            Assert.False(PosterDropSurfacePolicy.ReceivesOle(frame, new(-900, -300 + height)));
        }
        finally { Assert.True(DeleteObject(region)); }
    }
    private sealed class Log : IStructuredLog
    {
        public List<string> Errors { get; } = [];
        public void Write(string level, string eventName, object? data = null) => Errors.Add(eventName + ": " + data);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void ActualOleRegistrationAndShellHelperReleaseTheirStaResources(bool sourceImage)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            nint window = 0, bitmap = 0;
            try
            {
                window = CreateWindowEx(0x08000080, "STATIC", "D3 isolated hidden target", 0x80000000,
                    -1000, -300, 600, 900, 0, 0, 0, 0);
                Assert.NotEqual(0, window);
                var ledger = new Ledger(); var log = new Log();
                // Real RegisterDragDrop, helper COM and RevokeDragDrop. No physical input/file move.
                var surface = IncomingDropSessionTests.Surface();
                using (var target = new NativeFileDropTarget(window, () => surface, _ => { }, ledger, log))
                {
                    Assert.Single(ledger.ActiveResources);
                    using var data = Data(); uint effect = 2;
                    using var source = sourceImage ? new ShellProbeImages() : null;
                    if (sourceImage)
                    {
                        bitmap = CreateBitmap(16, 16, 1, 32, Enumerable.Repeat((byte)255, 1024).ToArray());
                        Assert.NotEqual(0, bitmap);
                        source!.InitializeSource(bitmap, 16, 16, 7, 9, data); bitmap = 0;
                    }
                    target.DragEnter(data, 0, Point, ref effect);
                    Assert.True(effect == 2u, string.Join("\n", log.Errors));
                    target.DragLeave(); Assert.False(target.IsHovering);
                    target.Dispose(); target.Dispose();
                }
                Assert.Empty(ledger.ActiveResources);
                // The exact same HWND must be registerable again, with no retained COM owner.
                using (var again = new NativeFileDropTarget(window, () => true, _ => { }, ledger, log))
                    Assert.Single(ledger.ActiveResources);
                Assert.Empty(ledger.ActiveResources);
                Assert.True(log.Errors.Count == 0, string.Join("\n", log.Errors));
            }
            catch (Exception e) { error = e; }
            finally { if (bitmap != 0) _ = DeleteObject(bitmap); if (window != 0) _ = DestroyWindow(window); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Native callback test did not return.");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private sealed class Ledger : IResourceLedger
    {
        private readonly HashSet<string> active = [];
        public IReadOnlyCollection<string> ActiveResources => active;
        public IDisposable Acquire(string resourceId, string kind)
        { Assert.True(active.Add(resourceId)); return new Lease(() => Assert.True(active.Remove(resourceId))); }
        public void WriteSnapshot() { }
        private sealed class Lease(Action end) : IDisposable
        { private bool closed; public void Dispose() { if (!closed) { closed = true; end(); } } }
    }
    private sealed class Images : INativeDropImages
    {
        public string? Failure; public uint DropEffect; public List<string> Events { get; } = [];
        private void Record(string name) { Events.Add(name); if (Failure == name) throw new IOException(name); }
        public void Enter(nint window, IDataObject data, FileDropPoint point, uint effect) => Record("enter");
        public void Over(FileDropPoint point, uint effect) => Record("over");
        public void Leave() => Record("leave");
        public void Drop(IDataObject data, FileDropPoint point, uint effect) { DropEffect = effect; Record("drop"); }
        public void Dispose() => Record("dispose");
    }
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    private static extern nint CreateWindowEx(uint extended, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("gdi32.dll")] private static extern nint CreateBitmap(int width, int height, uint planes, uint bits, byte[] pixels);
}
