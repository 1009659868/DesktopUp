using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class DesktopVisualSurfaceReadbackTests
{
    private static readonly DesktopPixelRect Surface = new(2049, 139, 240, 360);
    private static readonly DesktopPixelRect Client = new(0, 0, 240, 360);

    [Theory]
    [InlineData(60)]
    [InlineData(290)]
    [InlineData(360)]
    public void CapacityAndVisibleClipAreIndependent(int height)
    {
        var visible = Surface with { Height = height };
        var actual = DesktopVisualSurfaceReadback.Compare(Surface, visible, Surface, Client,
            Client with { Height = height }, 3, 3);
        Assert.True(actual.Verified);
        Assert.Null(actual.Failure);
        Assert.Contains("2049", JsonSerializer.Serialize(actual));
    }

    [Theory]
    [InlineData("window")]
    [InlineData("client")]
    [InlineData("region")]
    public void MismatchKeepsEveryBoundaryFromTheRejectingRead(string boundary)
    {
        var window = boundary == "window" ? Surface with { X = 2152 } : Surface;
        var client = boundary == "client" ? Client with { Height = 350 } : Client;
        var region = boundary == "region" ? Client with { X = 1, Width = 239 } : Client;
        var actual = DesktopVisualSurfaceReadback.Compare(Surface, Surface, window, client, region, 3, 3);
        Assert.False(actual.Verified);
        Assert.Equal(boundary + "-bounds-mismatch", actual.Failure);
        Assert.Equal(window, actual.Window);
        Assert.Equal(client, actual.Client);
        Assert.Equal(region, actual.Region);
    }

    [Theory]
    [InlineData(0, "window-read-failed")]
    [InlineData(1, "client-read-failed")]
    [InlineData(2, "region-allocation-failed")]
    [InlineData(3, "window-region-read-failed")]
    [InlineData(4, "region-box-read-failed")]
    public void MissingApiResultsFailClosedAndAreNotZeroRectangles(int failure, string reason)
    {
        var actual = DesktopVisualSurfaceReadback.Compare(Surface, Surface,
            failure == 0 ? null : Surface, failure == 1 ? null : Client, null,
            failure == 2 ? null : failure == 3 ? 0 : 3, failure == 4 ? 0 : null);
        Assert.False(actual.Verified);
        Assert.Equal(reason, actual.Failure);
        Assert.Null(actual.Region);
    }

    [Fact]
    public void BadCapacityNeverBecomesValidBecauseItsNativeReadMatches()
    {
        var actual = DesktopVisualSurfaceReadback.Compare(Surface, Surface with { Height = 361 },
            Surface, Client, Client, 3, 3);
        Assert.False(actual.Verified);
        Assert.Equal("surface-contract-invalid", actual.Failure);
    }

    [Fact]
    public void InvalidNativeWindowReportsReadFailureWithoutThrowing()
    {
        var actual = new DesktopWindowNativeApi().InspectVisualSurface(0, Surface, Surface);
        Assert.False(actual.Verified);
        Assert.Equal("window-read-failed", actual.Failure);
        Assert.Null(actual.Window);
    }
}
