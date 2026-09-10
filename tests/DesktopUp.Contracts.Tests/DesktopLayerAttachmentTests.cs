using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class DesktopLayerAttachmentTests
{
    private static readonly DesktopShellHostIdentity HostIdentity = new(200, 300, 400);
    private static readonly DesktopShellHostSnapshot Host =
        new(HostIdentity, 7, DateTimeOffset.UtcNow);

    [Fact]
    public void AttachAppliesOwnerStylesAndReadBack()
    {
        var windows = new FakeDesktopWindowApi();
        var desktop = new FakeExplorerDesktopApi();
        var attachment = new DesktopLayerAttachment(windows, desktop);

        DesktopLayerAttachResult result = attachment.Attach(100, Host);

        Assert.True(result.Succeeded);
        Assert.Equal(HostIdentity.IconViewHandle, result.ActualOwner);
        Assert.Equal(7, result.DesktopGeneration);
        Assert.Equal(1, windows.FrameApplyCount);
        Assert.Equal(1, windows.BottomPlacementCount);
        Assert.False(windows.LastBottomPlacementShowsWindow);
        Assert.True(windows.BorderRemoved);
    }

    [Fact]
    public void InitialAttachCanShowAtomicallyAtDesktopBottom()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());

        DesktopLayerAttachResult result = attachment.Attach(
            100,
            Host,
            showWindow: true);

        Assert.True(result.Succeeded);
        Assert.True(windows.LastBottomPlacementShowsWindow);
        Assert.Equal(1, windows.BottomPlacementCount);
    }

    [Fact]
    public void InputTransparentAttachUsesLayeredCompositionAndPreservesItOnReassert()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());

        Assert.True(attachment.Attach(
            100,
            Host,
            showWindow: true,
            inputTransparent: true).Succeeded);
        Assert.Equal(1, windows.TransparentCompositionApplyCount);
        Assert.Equal(0x00080020, windows.ExtendedStyle.ToInt64() & 0x00080020);

        windows.ExtendedStyle = new nint(
            windows.ExtendedStyle.ToInt64() & ~0x00080020);
        Assert.False(attachment.IsAttached(100, Host));
        Assert.True(attachment.ReassertDesktopBottom(100, Host));
        Assert.Equal(2, windows.TransparentCompositionApplyCount);
        Assert.Equal(0x00080020, windows.ExtendedStyle.ToInt64() & 0x00080020);
    }

    [Fact]
    public void PointerPathReassertsBottomWithoutShowingOrChangingGeneration()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());
        Assert.True(attachment.Attach(100, Host, showWindow: true).Succeeded);

        windows.ExtendedStyle = new nint(windows.ExtendedStyle.ToInt64() | 0x08);

        Assert.True(attachment.ReassertDesktopBottom(100, Host));
        Assert.Equal(2, windows.BottomPlacementCount);
        Assert.False(windows.LastBottomPlacementShowsWindow);
        Assert.Equal(2, windows.FrameApplyCount);
        Assert.True(attachment.IsAttached(100, Host));
    }

    [Fact]
    public void RoundedRegionRejectsInvalidGeometryAndForwardsValidPhysicalBounds()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());

        Assert.False(attachment.ApplyRoundedRegion(100, 0, 700, 8));
        Assert.False(attachment.ApplyRoundedRegion(100, 400, 700, 201));
        Assert.True(attachment.ApplyRoundedRegion(100, 400, 700, 8));

        Assert.Equal(1, windows.RoundedRegionApplyCount);
        Assert.Equal((400, 700, 8), windows.LastRoundedRegion);
    }

    [Fact]
    public void PreparedBoundsRequireAttachmentAndUseNoEraseNativeCommit()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());
        var bounds = new DesktopPixelRect(100, 120, 420, 700);

        Assert.False(attachment.ApplyPreparedBounds(100, bounds));
        Assert.True(attachment.Attach(100, Host).Succeeded);
        Assert.True(attachment.ApplyPreparedBounds(100, bounds));

        Assert.Equal(1, windows.PreparedBoundsApplyCount);
        Assert.Equal(bounds, windows.LastPreparedBounds);
    }

    [Fact]
    public void BottomReassertRejectsStaleDesktopGeneration()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());
        Assert.True(attachment.Attach(100, Host).Succeeded);

        Assert.False(attachment.ReassertDesktopBottom(
            100,
            Host with { Generation = Host.Generation + 1 }));
        Assert.Equal(1, windows.BottomPlacementCount);
    }

    [Fact]
    public void AttachIsIdempotentAndDetachRestoresOriginalOwnerAndStyles()
    {
        var windows = new FakeDesktopWindowApi
        {
            Owner = 55,
            Style = new nint(0x00C40000),
            ExtendedStyle = new nint(0x08)
        };
        var attachment = new DesktopLayerAttachment(windows, new FakeExplorerDesktopApi());

        Assert.True(attachment.Attach(100, Host).Succeeded);
        Assert.True(attachment.Attach(100, Host).Succeeded);
        Assert.True(attachment.Detach(100));
        Assert.True(attachment.Detach(100));

        Assert.Equal(new nint(55), windows.Owner);
        Assert.Equal(new nint(0x00C40000), windows.Style);
        Assert.Equal(new nint(0x08), windows.ExtendedStyle);
    }

    [Fact]
    public void OwnerReadBackFailureRollsBackImmediately()
    {
        var windows = new FakeDesktopWindowApi
        {
            Owner = 55,
            IgnoreOwnerWrites = true
        };
        var attachment = new DesktopLayerAttachment(windows, new FakeExplorerDesktopApi());

        DesktopLayerAttachResult result = attachment.Attach(100, Host);

        Assert.False(result.Succeeded);
        Assert.Equal("owner-readback-mismatch", result.Error);
        Assert.Equal(new nint(55), windows.Owner);
    }

    [Fact]
    public void InvalidDesktopHostNeverMutatesPosterWindow()
    {
        var windows = new FakeDesktopWindowApi { Owner = 55 };
        var desktop = new FakeExplorerDesktopApi { Valid = false };
        var attachment = new DesktopLayerAttachment(windows, desktop);

        DesktopLayerAttachResult result = attachment.Attach(100, Host);

        Assert.False(result.Succeeded);
        Assert.Equal("desktop-host-invalid", result.Error);
        Assert.Equal(new nint(55), windows.Owner);
        Assert.Equal(0, windows.FrameApplyCount);
    }

    [Fact]
    public void NewDesktopGenerationSupersedesOldAttachmentState()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(
            windows,
            new FakeExplorerDesktopApi());
        var nextGeneration = Host with { Generation = 8 };

        Assert.True(attachment.Attach(100, Host).Succeeded);
        Assert.True(attachment.IsAttached(100, Host));
        Assert.True(attachment.Attach(100, nextGeneration).Succeeded);

        Assert.False(attachment.IsAttached(100, Host));
        Assert.True(attachment.IsAttached(100, nextGeneration));
    }

    [Fact]
    public void BatchValidatesEveryParticipantBeforeNativeCommitAndNeverFallsBackToSequentialMoves()
    {
        Assert.Equal(0x0314u, DesktopZOrderPolicy.SpatialBatchFlags);
        Assert.Equal(0u, DesktopZOrderPolicy.SpatialBatchFlags & 0x2000u); // Unsupported by DeferWindowPos.
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(windows, new FakeExplorerDesktopApi());
        Assert.True(attachment.Attach(100, Host).Succeeded);
        var frames = new Dictionary<nint, DesktopPixelRect> { [100] = new(0, 0, 300, 300), [101] = new(0, 300, 300, 300) };
        Assert.False(attachment.ApplyPreparedBoundsBatch(frames)); Assert.Equal(0, windows.BatchCount);
        Assert.True(attachment.Attach(101, Host).Succeeded);
        Assert.True(attachment.ApplyPreparedBoundsBatch(frames)); Assert.Equal(1, windows.BatchCount);
        windows.FailBatch = true;
        Assert.False(attachment.ApplyPreparedBoundsBatch(frames)); Assert.Equal(2, windows.BatchCount);
        Assert.Equal(0, windows.PreparedBoundsApplyCount);
        Assert.False(attachment.ApplyPreparedBoundsBatch(new Dictionary<nint, DesktopPixelRect>()));
    }

    [Fact]
    public void PresentationPhasesOnlySuppressVisualsAndFinalRedrawValidatesTheWholeSet()
    {
        var windows = new FakeDesktopWindowApi();
        var attachment = new DesktopLayerAttachment(windows, new FakeExplorerDesktopApi());
        Assert.True(attachment.Attach(100, Host, inputTransparent: true).Succeeded);
        Assert.True(attachment.Attach(101, Host).Succeeded);
        var frames = new Dictionary<nint, DesktopPixelRect> { [100] = new(0, 0, 300, 200), [101] = new(0, 0, 300, 200) };
        Assert.True(attachment.ApplyPreparedBoundsBatch(frames, DesktopFramePhase.Animation));
        Assert.Equal(0x031du, windows.LastBatchFlags![100]);
        Assert.Equal(0x0314u, windows.LastBatchFlags[101]);
        Assert.True(attachment.ApplyPreparedBoundsBatch(frames, DesktopFramePhase.FinalRestore));
        Assert.Equal(0x0214u, windows.LastBatchFlags[100]);
        Assert.Equal(0x0314u, windows.LastBatchFlags[101]);
        Assert.True(attachment.ApplyPreparedBoundsBatch(frames, DesktopFramePhase.SurfacePreparation));
        Assert.Equal(0x0214u, windows.LastBatchFlags[100]);
        Assert.Equal(0x0314u, windows.LastBatchFlags[101]);
        Assert.False(attachment.CompletePreparedVisualFrame([100, 101]));
        Assert.Empty(windows.Redrawn);
        Assert.False(attachment.CompletePreparedVisualFrame([100, 999]));
        Assert.True(attachment.CompletePreparedVisualFrame([100]));
        Assert.Equal(new nint[] { 100 }, windows.Redrawn);
        windows.FailBatch = true;
        Assert.False(attachment.ApplyPreparedBoundsBatch(frames, DesktopFramePhase.Animation));
        Assert.Equal(0, windows.PreparedBoundsApplyCount);
        Assert.False(attachment.ApplyPreparedBoundsBatch(frames, (DesktopFramePhase)99));
        windows.FailRedraw = true;
        Assert.False(attachment.CompletePreparedVisualFrame([100]));
    }

    private sealed class FakeExplorerDesktopApi : IExplorerDesktopApi
    {
        public bool Valid { get; set; } = true;
        public bool TryFindExistingHost(out DesktopShellHostIdentity identity)
        {
            identity = HostIdentity;
            return Valid;
        }
        public bool IsValid(DesktopShellHostIdentity identity) => Valid && identity == HostIdentity;
    }

    private sealed class FakeDesktopWindowApi : IDesktopWindowApi
    {
        public nint Owner { get; set; }
        public nint Style { get; set; } = new(0x00C40000);
        public nint ExtendedStyle { get; set; } = new(0x08);
        public bool IgnoreOwnerWrites { get; set; }
        public int FrameApplyCount { get; private set; }
        public int BottomPlacementCount { get; private set; }
        public bool LastBottomPlacementShowsWindow { get; private set; }
        public bool BorderRemoved { get; private set; }
        public int TransparentCompositionApplyCount { get; private set; }
        public int RoundedRegionApplyCount { get; private set; }
        public int PreparedBoundsApplyCount { get; private set; }
        public (int Width, int Height, int Radius) LastRoundedRegion { get; private set; }
        public DesktopPixelRect LastPreparedBounds { get; private set; }
        public int BatchCount { get; private set; }
        public bool FailBatch { get; set; }
        public bool FailRedraw { get; set; }
        public IReadOnlyDictionary<nint, uint>? LastBatchFlags { get; private set; }
        public List<nint> Redrawn { get; } = [];
        public bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds, IReadOnlyDictionary<nint, uint> flags)
        { LastBatchFlags = flags; BatchCount++; return !FailBatch; }
        public bool RequestFrameRedraw(nint windowHandle) { Redrawn.Add(windowHandle); return !FailRedraw; }
        public bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds)
        { BatchCount++; return !FailBatch; }

        public bool IsWindow(nint windowHandle) => windowHandle != 0;
        public nint GetOwner(nint windowHandle) => Owner;
        public nint SetOwner(nint windowHandle, nint ownerHandle)
        {
            nint previous = Owner;
            if (!IgnoreOwnerWrites)
            {
                Owner = ownerHandle;
            }
            return previous;
        }
        public nint GetStyle(nint windowHandle) => Style;
        public nint GetExtendedStyle(nint windowHandle) => ExtendedStyle;
        public nint SetStyle(nint windowHandle, nint style)
        {
            nint previous = Style;
            Style = style;
            return previous;
        }
        public nint SetExtendedStyle(nint windowHandle, nint style)
        {
            nint previous = ExtendedStyle;
            ExtendedStyle = style;
            return previous;
        }
        public bool ApplyFrameAndNonTopmost(nint windowHandle)
        {
            FrameApplyCount++;
            return true;
        }
        public bool PlaceAtDesktopSiblingBottom(nint windowHandle, bool showWindow)
        {
            BottomPlacementCount++;
            LastBottomPlacementShowsWindow = showWindow;
            return true;
        }
        public bool ApplyInputTransparentComposition(nint windowHandle)
        {
            TransparentCompositionApplyCount++;
            return true;
        }
        public bool ApplyRoundedRegion(
            nint windowHandle,
            int width,
            int height,
            int cornerRadius)
        {
            RoundedRegionApplyCount++;
            LastRoundedRegion = (width, height, cornerRadius);
            return true;
        }
        public bool ApplyPreparedBounds(
            nint windowHandle,
            DesktopPixelRect bounds)
        {
            PreparedBoundsApplyCount++;
            LastPreparedBounds = bounds;
            return true;
        }
        public void RemoveDwmBorder(nint windowHandle) => BorderRemoved = true;
        public int GetLastError() => 0;
    }
}
