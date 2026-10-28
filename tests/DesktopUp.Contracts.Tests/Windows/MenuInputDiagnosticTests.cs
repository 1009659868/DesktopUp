using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class MenuInputDiagnosticTests
{
    [Theory]
    [InlineData(null, @"G:\isolated", false)]
    [InlineData("0", @"G:\isolated", false)]
    [InlineData("1", null, false)]
    [InlineData("1", "relative", false)]
    [InlineData("1", @"C:\profile\DesktopUp\V3", false)]
    [InlineData("1", @"c:\PROFILE\desktopup\v3\", false)]
    [InlineData("1", @"C:\profile\DesktopUp\V3\child", false)]
    [InlineData("1", @"G:\isolated", true)]
    public void LoggingRequiresExplicitOptInOutsideNormalProfile(string? flag, string? root, bool expected) =>
        Assert.Equal(expected, MenuInputDiagnostics.AllowsIsolatedTrace(flag, root, @"C:\profile\DesktopUp\V3"));

    [Fact]
    public void DiagnosticDoesNotMutateWindowInputOrCommandPolicy()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string path) => File.ReadAllText(Path.Combine(root.FullName, path));
        string native = Read("src/DesktopUp.Windows/Diagnostics/MenuInputDiagnostics.cs");
        string menu = Read("src/DesktopUp.App/Diagnostics/MenuInputDiagnostic.cs");
        foreach (string forbidden in new[] { "SetWindowPos(", "SetWindowLong", "SetCapture(", "SendInput(", "PostMessage(", "SendMessage(", "SetWindowRgn(", "SetWindowsHook" })
            Assert.DoesNotContain(forbidden, native, StringComparison.Ordinal);
        Assert.DoesNotContain(".Handled =", menu, StringComparison.Ordinal);
        Assert.DoesNotContain(".IsHitTestVisible =", menu, StringComparison.Ordinal);
        Assert.Contains("handledEventsToo: true", menu, StringComparison.Ordinal);
        Assert.Contains("GetBoundingRectangle()", menu, StringComparison.Ordinal);
        Assert.Contains("NativeMessage -= Native", menu, StringComparison.Ordinal);
        Assert.Contains("_records >= 1200", menu, StringComparison.Ordinal);
        string poster = Read("src/DesktopUp.App/Views/Poster/PosterVisualWindow.cs");
        Assert.Contains("bool accepted = !_disposed && _posterEntityVersion == menuEntity && inputRouter.IsCurrent(inputSession, menuLayout) &&", poster, StringComparison.Ordinal);
        Assert.Contains("!spatial.HasCenterDraft(inputSession.PosterId)", poster, StringComparison.Ordinal);
        Assert.Contains("inputTransparent: true", poster, StringComparison.Ordinal);
    }
}
