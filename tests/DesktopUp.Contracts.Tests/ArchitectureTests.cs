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
        Assert.DoesNotContain("DesktopUp.Infrastructure", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Domain", "")]
    [InlineData("Application", "Domain")]
    [InlineData("Infrastructure", "Application,Domain")]
    [InlineData("Windows", "Application,Domain")]
    [InlineData("ContentWorker", "Application")]
    [InlineData("App", "Application,ContentWorker,Domain,Infrastructure,Windows")]
    public void ProductionProjectReferencesFollowDeclaredLayers(string project, string allowed)
    {
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(FindRepositoryRoot(),
            "src", "DesktopUp." + project, "DesktopUp." + project + ".csproj"));
        var references = document.Descendants("ProjectReference")
            .Select(node => Path.GetFileNameWithoutExtension(((string)node.Attribute("Include")!)
                .Replace('\\', '/')).Replace("DesktopUp.", "", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(allowed.Split(',', StringSplitOptions.RemoveEmptyEntries), references);
        if (project == "App")
        {
            var worker = document.Descendants("ProjectReference").Single(node =>
                ((string)node.Attribute("Include")!).Contains("ContentWorker", StringComparison.Ordinal));
            Assert.Equal("false", (string?)worker.Attribute("ReferenceOutputAssembly"));
        }
    }

    [Fact]
    public void ContentExecutionDoesNotOwnPresentationAndPosterDoesNotOwnOpenJournal()
    {
        string root = FindRepositoryRoot();
        string service = File.ReadAllText(Path.Combine(root,
            "src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs"));
        foreach (string forbidden in new[] { "ReferenceContentRuntime", "IContentAssetProvider",
            "ContentSourcePorts", "Sqlite", "Microsoft.UI" })
            Assert.DoesNotContain(forbidden, service, StringComparison.Ordinal);
        string adapter = File.ReadAllText(Path.Combine(root,
            "src/DesktopUp.Application/Features/Content/Open/ReferenceContentActions.cs"));
        Assert.Contains("IContentActionService", adapter);
        Assert.DoesNotContain("IContentOpenJournal", adapter);
        Assert.DoesNotContain("ILocalContentWorker", adapter);
        string poster = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterSpatialRuntime.ContentSource.cs"));
        Assert.DoesNotContain("IContentOpenJournal", poster);
        Assert.Contains("IContentActionService", poster);
    }

    [Fact]
    public void HeatRuntimeOwnsDerivedStateButNotWindowSchedulingOrPersistence()
    {
        string root = FindRepositoryRoot();
        string session = File.ReadAllText(Path.Combine(root,
            "src/DesktopUp.Application/Features/Content/Layout/ContentHeatSession.cs"));
        foreach (string forbidden in new[] { "DispatcherQueue", "Sqlite", "IStateCommandService",
            "PosterVisualWindow", "Timer", "IContentAssetProvider" })
            Assert.DoesNotContain(forbidden, session, StringComparison.Ordinal);
        string host = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs"));
        Assert.Contains("ContentHeatSession", host);
        Assert.Contains("ContentProposalsProtected", host);
        Assert.Contains("RefreshContentSurface()", host);
        Assert.DoesNotContain("ContentLayoutProposals.ResolveHeat", host);
        Assert.DoesNotContain("ContentHeatProposalBuffer", host);
    }

    [Fact]
    public void FileActionVerticalSliceUsesApplicationPortsAndBootstrapComposition()
    {
        string root = FindRepositoryRoot();
        var surfaces = string.Join("\n", new[] { "PosterVisualWindow.FileActions.cs",
            "PosterVisualWindow.NewContent.cs", "PosterVisualWindow.InlineRename.cs",
            "PosterSpatialRuntime.FileActions.cs" }.Select(name =>
                File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", name))));
        foreach (string forbidden in new[] { "DesktopUp.Infrastructure", "Sqlite", "LocalTransferFiles",
            "new ContentCreationService", "new ContentRenameService", "new ContentDeletionService", "new ShellUiClient" })
            Assert.DoesNotContain(forbidden, surfaces, StringComparison.Ordinal);
        Assert.Contains("IContentFileActionService", surfaces);
        string bootstrap = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Bootstrap/App.ContentServices.cs"));
        foreach (string useCase in new[] { "ContentCreationUseCase", "ContentRenameUseCase", "ContentDeletionUseCase", "ConfigureFileActions" })
            Assert.Contains(useCase, bootstrap);
    }

    [Fact]
    public void TransferVerticalSliceUsesApplicationOrchestrationAndNoConcreteStorage()
    {
        string root = FindRepositoryRoot();
        var surfaces = string.Join("\n", new[] { "PosterSpatialRuntime.Incoming.cs", "PosterSpatialRuntime.NativeExit.cs",
            "PosterSpatialRuntime.TransferUseCases.cs", "PosterVisualWindow.Transfers.cs" }.Select(name =>
                File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", name))));
        foreach (string forbidden in new[] { "DesktopUp.Infrastructure", "Sqlite", "LocalTransferFiles", "_transferStore" })
            Assert.DoesNotContain(forbidden, surfaces, StringComparison.Ordinal);
        string bootstrap = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Bootstrap/App.ContentServices.cs"));
        foreach (string useCase in new[] { "ManagedIngressUseCase", "MappedIngressUseCase", "CrossPosterTransferUseCase", "NativeShellExitUseCase" })
        {
            Assert.Contains(useCase, bootstrap);
            Assert.Contains(useCase, surfaces);
        }
        string exit = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Application/Features/Content/Transfers/NativeShellExitUseCase.cs"));
        Assert.DoesNotContain("IShellDirectoryTransfer", exit);
        Assert.DoesNotContain("File.Move", exit);
        Assert.DoesNotContain("File.Delete", exit);
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
                "ControlCenterWindow.AutoPosture.cs",
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
    public void PosterResizeKeepsBackdropAndDeferredBoundsCommitContracts()
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

        Assert.Contains("ApplyWindowBackdrop(window)", poster, StringComparison.Ordinal);
        Assert.Contains("attachment.ApplyPreparedBounds", poster, StringComparison.Ordinal);
        Assert.Contains("WmEraseBackground", watcher, StringComparison.Ordinal);
        Assert.Contains("FillRect", watcher, StringComparison.Ordinal);
        Assert.Contains("SwpNoCopyBits", attachment, StringComparison.Ordinal);
        Assert.Contains("SwpDeferErase", attachment, StringComparison.Ordinal);
    }

    [Fact]
    public void PosterFillIsWindowScopedAndUpdatedWithPresentation()
    {
        string root = FindRepositoryRoot();
        string backdrop = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterVisualWindow.Backdrop.cs"));
        string visual = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterVisualWindow.cs"));
        string presentation = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterVisualWindow.Presentation.cs"));
        Assert.Contains("new WinUIEx.TransparentTintBackdrop(color)", backdrop);
        Assert.DoesNotContain("static WinUIEx.TransparentTintBackdrop", backdrop);
        Assert.Contains("_windowBackdrop.TintColor = color", backdrop);
        Assert.Contains("CurrentSceneParameters.Surface", backdrop);
        Assert.Contains("ColorBrush(paint.Background, paint.BackgroundAlpha)", backdrop);
        string paint = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Application/ScenePresentationAdapter.cs"));
        Assert.Contains("new SceneSurfacePaint(presentation.Parameters.Theme.Background, 245, null)", paint);
        Assert.DoesNotContain("LampTemplate", backdrop);
        Assert.Contains("window.SystemBackdrop = _windowBackdrop", backdrop);
        Assert.Contains("Background = new SolidColorBrush(Colors.Transparent)", visual);
        Assert.DoesNotContain("Background = ColorBrush(_presentation.Parameters.Theme.Background, 245)", visual);
        Assert.Contains("ApplyWindowBackdrop(_window)", presentation);
        Assert.Contains("VerifyWindowBackdrop()", presentation);
    }

    [Fact]
    public void Shared_scene_host_does_not_reference_a_concrete_lamp_module()
    {
        string root = FindRepositoryRoot();
        foreach (string path in new[]
        {
            "PosterVisualWindow.SceneHost.cs", "PosterVisualWindow.SurfacePaint.cs",
            "PosterSpatialRuntime.SceneMotion.cs", "PosterVisualWindow.Backdrop.cs"
        })
        {
            string source = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", path));
            Assert.DoesNotContain("Lamp", source, StringComparison.Ordinal);
            Assert.DoesNotContain("lamp", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Rain", source, StringComparison.Ordinal);
            Assert.DoesNotContain("rain", source, StringComparison.Ordinal);
        }
        string painter = File.ReadAllText(Path.Combine(root, "Modules/Lamp/App/LampSceneInstance.cs"));
        foreach (string forbidden in new[] { "PosterSpatialRuntime", "PosterInputRouter", "DispatcherQueueTimer",
            "IStateCommandService", "SetWindowRgn", "SetCapture", "ContentLayoutSolver" })
        {
            Assert.DoesNotContain(forbidden, painter, StringComparison.Ordinal);
            Assert.DoesNotContain(forbidden, File.ReadAllText(Path.Combine(root, "Modules/Rain/App/RainSceneInstance.cs")), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BackdropOwnsEraseWhileActiveAndDisconnectsBeforeWindowClose()
    {
        string root = FindRepositoryRoot();
        string backdrop = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterVisualWindow.Backdrop.cs"));
        string visual = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/PosterVisualWindow.cs"));
        string watcher = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Windows/DesktopEnvironmentWatcher.cs"));
        string session = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/WinUiPosterSession.cs"));
        Assert.Contains("compositionOwnsBackground: () => visual.HasWindowBackdrop", session);
        Assert.Contains("_compositionOwnsBackground?.Invoke() != true", watcher);
        Assert.Contains("return DefSubclassProc", watcher);
        Assert.Contains("_window.SystemBackdrop = null", backdrop);
        Assert.Contains("_windowBackdrop = null", backdrop);
        string dispose = visual.Split("public void Dispose()")[1];
        Assert.True(dispose.IndexOf("Cleanup(ReleaseWindowBackdrop)", StringComparison.Ordinal) <
                    dispose.IndexOf("_window.Close()", StringComparison.Ordinal));
    }

    [Fact]
    public void DailyContentEntryUsesDragAndScopedManagementInsteadOfAddWizard()
    {
        string root = FindRepositoryRoot();
        var xaml = System.Xml.Linq.XDocument.Load(Path.Combine(root, "src/DesktopUp.App/ControlCenterWindow.xaml"));
        var controls = xaml.Descendants().ToArray();
        Assert.DoesNotContain(controls, e => e.Attributes().Any(a => a.Value == "AddReferences_Click" || a.Value == "AddContentReferences"));
        Assert.Single(controls.Where(e => e.Attributes().Any(a => a.Value == "ContentManageButton")));
        Assert.DoesNotContain(controls, e => e.Attributes().Any(a => a.Value == "RepairReference_Click"));
        string menu = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/ControlCenterWindow.UiCWorkspace.cs"));
        Assert.Contains("ContentKeyboardPad.ContextFlyout?.ShowAt(ContentManageButton)", menu);
        Assert.Contains("ForSameItem(() => _ = EditReferences(true))", menu);
        Assert.Contains("view?.SelectedIds.Count == 1", menu);
        Assert.DoesNotContain("全选引用", menu);
        Assert.DoesNotContain("从此海报移除", menu);
        Assert.Single(controls.Where(e => e.Attributes().Any(a => a.Value == "ManagedStorage_Click")));
        Assert.DoesNotContain(controls, e => e.Attributes().Any(a => a.Value == "内容介绍文字"));
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
