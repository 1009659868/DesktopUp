using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DesktopUp.NativeDragProbe;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class NativeDragProbeTests
{
    [Theory]
    [InlineData(0u, true, 2u)]
    [InlineData(4u, true, 2u)]
    [InlineData(8u, true, 0u)]
    [InlineData(32u, true, 0u)]
    [InlineData(2u, true, 0u)]
    [InlineData(0u, false, 0u)]
    public void ImagesRemainInLifecycleEvenWhenDropIsRefused(uint keys, bool enabled, uint expected)
    {
        using var data = new ProbeDataObject([1, 2, 3]);
        var images = new Images();
        using var target = new ProbeDropTarget(1, images, (_, _) => { }, () => enabled);
        uint effect = 3;
        target.DragEnter(data, keys, new() { X = -130, Y = 105 }, ref effect);
        Assert.Equal(expected, effect);
        effect = 3; target.DragOver(keys, new() { X = 2048, Y = -15 }, ref effect);
        Assert.Equal(expected, effect);
        effect = 3; target.Drop(data, keys, new() { X = 2050, Y = -15 }, ref effect);
        Assert.Equal(0u, effect); Assert.False(target.Active);
        Assert.Equal(expected == 2 ? 1 : 0, target.ObservedDrops);
        Assert.Equal(new[] { "enter", "over", "drop" }, images.Events);
        Assert.Equal(2048, images.LastOver.X); Assert.Equal(-15, images.LastOver.Y);
        Assert.Equal(0u, images.DropEffect);
        effect = 3; target.Drop(data, keys, default, ref effect);
        Assert.Equal(expected == 2 ? 1 : 0, target.ObservedDrops);
        Assert.Equal(3, images.Events.Count);
    }
    [Theory]
    [InlineData("enter")]
    [InlineData("over")]
    [InlineData("drop")]
    [InlineData("leave")]
    [InlineData("dispose")]
    public void ImageFailureCannotEnableFileActionOrLeakSession(string stage)
    {
        using var data = new ProbeDataObject([1]);
        var images = new Images { Failure = stage }; var errors = new List<string>();
        var target = new ProbeDropTarget(1, images, (name, _) => errors.Add(name), () => true);
        uint effect = 2; target.DragEnter(data, 0, default, ref effect);
        effect = 2; target.DragOver(0, default, ref effect);
        if (stage == "drop") { effect = 2; target.Drop(data, 0, default, ref effect); Assert.Equal(0u, effect); }
        target.Dispose(); target.Dispose();
        Assert.False(target.Active); Assert.Contains("image-error", errors);
        Assert.Equal(1, images.Events.Count(value => value == "dispose"));
    }
    [Fact]
    public void ReenterClosesPreviousOwnerAndStalePayloadDoesNotDrop()
    {
        using var first = new ProbeDataObject([1]); using var second = new ProbeDataObject([2]);
        var images = new Images(); using var target = new ProbeDropTarget(1, images, (_, _) => { }, () => true);
        uint effect = 2; target.DragEnter(first, 0, default, ref effect);
        effect = 2; target.DragEnter(second, 0, default, ref effect);
        Assert.Equal(new[] { "enter", "leave", "enter" }, images.Events);
        effect = 2; target.Drop(first, 0, default, ref effect);
        Assert.Equal(0u, effect); Assert.Equal(0, target.ObservedDrops);
        Assert.False(target.Active);
    }
    [Fact]
    public void DisposedTargetCannotReenterOrDrop()
    {
        using var data = new ProbeDataObject([1]); var images = new Images();
        var target = new ProbeDropTarget(1, images, (_, _) => { }, () => true); target.Dispose();
        uint effect = 2; target.DragEnter(data, 0, default, ref effect); Assert.Equal(0u, effect);
        effect = 2; target.Drop(data, 0, default, ref effect);
        Assert.Equal(0u, effect); Assert.Equal(0, target.ObservedDrops); Assert.Equal(new[] { "dispose" }, images.Events);
    }
    [Theory]
    [InlineData(0x40100, 2u, 2u)]
    [InlineData(0x40100, 0u, 0u)]
    [InlineData(0x40100, 1u, 2u)]
    [InlineData(0x40101, 2u, 2u)]
    [InlineData(unchecked((int)0x80004005), 2u, 2u)]
    public void EffectPairIsNeverPermissionForSourceDeletion(int result, uint effect, uint performed)
    { Assert.EndsWith("no-cleanup", ProbeOutgoingVerdict.Classify(result, effect, performed)); }
    [Theory]
    [InlineData(true, 1u, false, 0x40101)]
    [InlineData(false, 1u, true, 0x40101)]
    [InlineData(false, 0u, false, 0x40100)]
    [InlineData(false, 1u, false, 0)]
    public void SourceGestureCancelsOnEscapeOrShutdown(bool escape, uint keys, bool cancelled, int expected)
    { Assert.Equal(expected, new ProbeSource((_, _) => { }, () => cancelled).QueryContinueDrag(escape, keys)); }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HelperPrivateFormatIsCopiedWithCorrectOwnership(bool release)
    {
        using var data = new ProbeDataObject([1]);
        var format = ProbeDataObject.Format(unchecked((short)ProbeDataObject.RegisterClipboardFormat("DesktopUp.D1.TestPrivate")));
        var medium = ProbeDataObject.Allocate([7, 8, 9, 10]);
        data.SetData(ref format, ref medium, release);
        if (!release) ProbeDataObject.ReleaseStgMedium(ref medium);
        Assert.True(ProbeDataObject.TryReadBytes(data, format.cfFormat, out var bytes));
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, bytes.Take(4));
        var hdrop = ProbeDataObject.Format(15); Assert.NotEqual(0, data.QueryGetData(ref hdrop));
    }
    [Fact]
    public void FormatEnumerationCloneAndBoundsAreStable()
    {
        using var data = new ProbeDataObject([1]); var enumerator = data.EnumFormatEtc(DATADIR.DATADIR_GET);
        var formats = new FORMATETC[1]; var count = new int[1];
        enumerator.Clone(out var clone);
        Assert.Equal(0, enumerator.Next(1, formats, count)); Assert.Equal(1, count[0]);
        Assert.Equal(1, enumerator.Next(1, formats, count)); Assert.Equal(0, count[0]);
        Assert.Equal(0, clone.Next(1, formats, count)); Assert.Equal(ProbeDataObject.PayloadFormat, formats[0].cfFormat);
        Assert.NotEqual(0, clone.Skip(-1)); Assert.Equal(0, clone.Reset());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Put(1, new byte[ProbeDataObject.MaximumBytes + 1]));
    }
    [Fact]
    public void NativeShellHelperAcceptsPrivateFormatsWithoutFilePayload()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            bool ole = false; nint bitmap = 0;
            var offered = new List<string>();
            try
            {
                Marshal.ThrowExceptionForHR(OleInitialize(0)); ole = true;
                using var data = new ProbeDataObject([1, 2]); using var helper = new ShellProbeImages();
                data.FormatOffered = (format, requested, medium, handle) => offered.Add($"{format}: {requested}/{medium}, null={handle == 0}");
                var written = new List<short>(); data.FormatWritten = (format, _) => written.Add(format);
                bitmap = CreateBitmap(16, 16, 1, 32, Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray());
                Assert.NotEqual(0, bitmap);
                helper.InitializeSource(bitmap, 16, 16, 7, 9, data); bitmap = 0; // Shell owns on success.
                Assert.NotEmpty(written);
                var enumerator = data.EnumFormatEtc(DATADIR.DATADIR_GET); var formats = new FORMATETC[1];
                while (enumerator.Next(1, formats, new int[1]) == 0)
                {
                    data.GetData(ref formats[0], out var medium);
                    Assert.NotEqual(0, medium.unionmember); ProbeDataObject.ReleaseStgMedium(ref medium);
                }
                Assert.Null(NativeFileDropTarget.ReadPaths(data));
                // Not a DoDragDrop / pixels / native pointer test.
            }
            catch (Exception exception) { error = new InvalidOperationException(string.Join("; ", offered), exception); }
            finally { if (bitmap != 0) DeleteObject(bitmap); if (ole) OleUninitialize(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private sealed class Images : IProbeDragImages
    {
        public string? Failure;
        public List<string> Events { get; } = [];
        public FileDropPoint LastOver;
        public uint DropEffect;
        private void Call(string name) { Events.Add(name); if (Failure == name) throw new COMException("Injected image failure"); }
        public void Enter(nint window, IDataObject data, FileDropPoint point, uint effect) => Call("enter");
        public void Over(FileDropPoint point, uint effect) { LastOver = point; Call("over"); }
        public void Leave() => Call("leave");
        public void Drop(IDataObject data, FileDropPoint point, uint effect) { DropEffect = effect; Call("drop"); }
        public void Dispose() => Call("dispose");
    }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("gdi32.dll")] private static extern nint CreateBitmap(int width, int height, uint planes, uint bits, byte[] pixels);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
}
