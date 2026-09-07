namespace DesktopUp.Contracts.Tests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("src/DesktopUp.Domain/DesktopUp.Domain.csproj")]
    [InlineData("src/DesktopUp.Application/DesktopUp.Application.csproj")]
    public void CoreProjectsDoNotReferenceWinUiOrWin32Adapters(string relativePath)
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        string text = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Prepend(projectPath)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("H.NotifyIcon", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DesktopUp.Windows", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.UI.Xaml", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DllImport", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LibraryImport", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecutableManifestRequestsOrdinaryUserPrivileges()
    {
        string root = FindRepositoryRoot();
        string manifest = File.ReadAllText(Path.Combine(root, "src", "DesktopUp.App", "app.manifest"));
        Assert.Contains("requestedExecutionLevel level=\"asInvoker\"", manifest, StringComparison.Ordinal);
        Assert.Contains("uiAccess=\"false\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void TrayContractKeepsNativeRightClickMenuEnabled()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "DesktopUp.App", "TrayService.cs"));
        Assert.Contains("ContextMenuMode.PopupMenu", source, StringComparison.Ordinal);
        Assert.Contains("PopupActivationMode.RightClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MenuActivation = PopupActivationMode.None", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UiSurfacesUseCommandsAndProjectionsInsteadOfRepositories()
    {
        string root = FindRepositoryRoot();
        string appDirectory = Path.Combine(root, "src", "DesktopUp.App");
        string surfaces = string.Join(
            Environment.NewLine,
            new[]
            {
                "PosterVisualWindow.cs",
                "ControlCenterService.cs",
                "ControlCenterWindow.xaml.cs",
                "TrayService.cs"
            }.Select(file => File.ReadAllText(Path.Combine(appDirectory, file))));

        Assert.DoesNotContain("IPosterRepository", surfaces, StringComparison.Ordinal);
        Assert.DoesNotContain("ISettingsRepository", surfaces, StringComparison.Ordinal);
        Assert.DoesNotContain("SqliteConnection", surfaces, StringComparison.Ordinal);
        Assert.Contains("StateProjectionChangedEventArgs", surfaces, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureInitialized(", surfaces, StringComparison.Ordinal);
        string bootstrap = File.ReadAllText(Path.Combine(appDirectory, "App.xaml.cs"));
        Assert.Contains("IStateProjectionCoordinator", bootstrap, StringComparison.Ordinal);
        Assert.Contains("PosterSessionRegistry", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("private PosterVisualWindow? _poster;", bootstrap, StringComparison.Ordinal);
        Assert.Contains("guardPosterWindow: false", bootstrap, StringComparison.Ordinal);
        Assert.Contains("IStateCommandService", surfaces, StringComparison.Ordinal);
        Assert.Contains("ControlCenterProjection", surfaces, StringComparison.Ordinal);
        Assert.Contains("TrayProjection", surfaces, StringComparison.Ordinal);
    }

    [Fact]
    public void PosterStartupUsesAtomicNativeBottomShowAndActivationGuard()
    {
        string root = FindRepositoryRoot();
        string poster = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.App",
            "PosterVisualWindow.cs"));
        string watcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "DesktopEnvironmentWatcher.cs"));

        Assert.Contains("showWindow: true", poster, StringComparison.Ordinal);
        Assert.DoesNotContain("AppWindow.Show(", poster, StringComparison.Ordinal);
        Assert.Contains("WmMouseActivate = 0x0021", watcher, StringComparison.Ordinal);
        Assert.Contains("MaNoActivate = 3", watcher, StringComparison.Ordinal);
        Assert.Contains("HtTransparent = -1", watcher, StringComparison.Ordinal);
        Assert.Contains("_reassertDesktopBottom()", watcher, StringComparison.Ordinal);
        Assert.Contains("WmActivate = 0x0006", watcher, StringComparison.Ordinal);
        Assert.Contains("WmActivateApp = 0x001C", watcher, StringComparison.Ordinal);
        Assert.Contains("WmWindowPosChanging = 0x0046", watcher, StringComparison.Ordinal);
        Assert.Contains("position.HwndInsertAfter = DesktopZOrderPolicy.HwndBottom", watcher, StringComparison.Ordinal);
    }

    [Fact]
    public void PosterResizePaintsBeforeADeferredNoCopyBoundsCommit()
    {
        string root = FindRepositoryRoot();
        string poster = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.App",
            "PosterVisualWindow.cs"));
        string watcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "DesktopEnvironmentWatcher.cs"));
        string attachment = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "DesktopLayerAttachment.cs"));

        Assert.Contains("Background = Brush(245, 33, 36, 42)", poster, StringComparison.Ordinal);
        Assert.Contains("attachment.ApplyPreparedBounds", poster, StringComparison.Ordinal);
        Assert.Contains("WmEraseBackground", watcher, StringComparison.Ordinal);
        Assert.Contains("FillRect", watcher, StringComparison.Ordinal);
        Assert.Contains("SwpNoCopyBits", attachment, StringComparison.Ordinal);
        Assert.Contains("SwpDeferErase", attachment, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("DesktopUp repository root was not found.");
    }
}
