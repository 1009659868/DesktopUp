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
        public void RemoveDwmBorder(nint windowHandle) => BorderRemoved = true;
        public int GetLastError() => 0;
    }
}
