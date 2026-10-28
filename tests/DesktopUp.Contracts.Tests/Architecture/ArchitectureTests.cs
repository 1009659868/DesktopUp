using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void ResponsiveLayoutKeepsReviewObservationOutOfProduction()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root,
            "src/DesktopUp.App/Views/ControlCenter/ControlCenterWindow.ResponsiveLayout.cs"));
        Assert.Contains("InitializeResponsiveLayout", source);
        Assert.Contains("ArrangeUiCShell", source);
        foreach (var reviewOnly in new[] { "UiCSampleObservation", "PrepareUiCSampleForProbe",
            "ReadUiCSampleForProbe", "ExpandLayoutFolderForProbe" })
            Assert.DoesNotContain(reviewOnly, source);
        var review = File.ReadAllText(Path.Combine(root,
            "tools/review/App/ControlCenterWindow.ResponsiveLayoutProbe.cs"));
        Assert.Contains("PrepareUiCSampleForProbe", review);
        Assert.Contains("ReadUiCSampleForProbe", review);
    }

    [Fact]
    public void ProductCompositionHasNoLocatorAndWorkerHasNoStorageOrSourceLinks()
    {
        var root = FindRepositoryRoot();
        var app = Path.Combine(root, "src/DesktopUp.App");
        // Exact composition exceptions; adding another App partial does not bypass this gate.
        var composition = new HashSet<string>(StringComparer.Ordinal)
        {
            "Bootstrap/ApplicationComposition.cs", "Bootstrap/App.Startup.cs", "Bootstrap/App.Configuration.cs",
            "Bootstrap/App.ContentServices.cs", "Bootstrap/App.ContentLifecycle.cs", "Bootstrap/App.ContentObservation.cs"
        };
        foreach (string path in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(app, path).Replace('\\', '/');
            if (relative.StartsWith("obj/") || relative.StartsWith("bin/")) continue;
            string source = File.ReadAllText(path);
            foreach (string token in new[] { "GetRequiredService", "ServiceProvider", "IServiceProvider" })
                Assert.DoesNotContain(token, source);
            if (composition.Contains(relative)) continue;
            foreach (string token in new[] { "DesktopUp.Infrastructure", "Sqlite", "new LocalContent",
                "new ShellUiClient", "new ShellDirectoryTransfer", "new WindowsStartupRegistration", ".Store" })
                Assert.DoesNotContain(token, source);
        }
        string worker = Path.Combine(root, "src/DesktopUp.ContentWorker");
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(worker, "DesktopUp.ContentWorker.csproj"));
        Assert.Empty(project.Descendants("Compile")); // No linked storage implementation under a platform-looking name.
        Assert.False(File.Exists(Path.Combine(root, "src/DesktopUp.Infrastructure/LocalContentReferenceProbe.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src/DesktopUp.Windows/Content/LocalContentReferenceProbe.cs")));
        foreach (string path in Directory.EnumerateFiles(worker, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(worker, path).Replace('\\', '/');
            if (relative.StartsWith("obj/") || relative.StartsWith("bin/")) continue;
            string source = File.ReadAllText(path);
            Assert.DoesNotContain("DesktopUp.Infrastructure", source);
            Assert.DoesNotContain("Sqlite", source);
        }
        string lifetime = File.ReadAllText(Path.Combine(app, "Lifecycle/App.Lifetime.cs"));
        int At(string value) => lifetime.IndexOf(value, StringComparison.Ordinal);
        Assert.True(At("StopTransfersAsync") < At("_posterSessions?.Dispose()"));
        Assert.True(At("_posterSessions?.Dispose()") < At("StopWorkerAsync"));
        Assert.True(At("StopWorkerAsync") < At("WaitForContentOperationsAsync"));
        Assert.True(At("WaitForContentOperationsAsync") < At("_composition?.Dispose()"));
    }

    [Fact]
    public void ContentRenderingOwnsProjectionButNotBusinessOrWindowLifetime()
    {
        var root = FindRepositoryRoot();
        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
        var presenter = Read("src/DesktopUp.App/Presentation/PosterContentPresenter.cs");
        var layout = Read("src/DesktopUp.Application/Features/Content/Layout/ContentPresentationLayout.cs");
        var host = Read("src/DesktopUp.App/Views/Poster/PosterVisualWindow.Content.cs");
        foreach (var forbidden in new[] { "Sqlite", "ContentFileAction", "IStateCommandService", "File.", "Directory.",
            "ContentLayoutSolver.Resolve", "DispatcherQueue" })
            Assert.DoesNotContain(forbidden, presenter);
        foreach (var forbidden in new[] { "Microsoft.UI", "DispatcherQueue", "Sqlite", "File.", "Directory." })
            Assert.DoesNotContain(forbidden, layout);
        Assert.DoesNotContain("record CardPaint", host);
        Assert.DoesNotContain("Dictionary<string, CardPaint>", host);
        Assert.DoesNotContain("ContentLayoutSolver.Resolve", host);
        Assert.DoesNotContain("AddContentInputRegions", host);
        Assert.Contains("Presenter.Present", host);
        Assert.Contains("_contentLayout.Prepare", host);
    }

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
    [InlineData("Infrastructure", "Application,Domain,Windows")]
    [InlineData("Windows", "Application,Domain")]
    [InlineData("ContentWorker", "Application,Windows")]
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
        string poster = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Runtime/Posters/PosterSpatialRuntime.ContentSource.cs"));
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
        string host = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.ContentProposals.cs"));
        Assert.Contains("_contentSession", host);
        Assert.Contains("BeginDrain", host);
        foreach (string oldState in new[] { "_contentHeatSession", "_contentThemeBuffer", "_contentProposalDrainQueued",
            "_contentAwaitingCenterRelease", "_contentProposalDrainEpoch", "private bool ContentProposalsProtected" })
            Assert.DoesNotContain(oldState, host);
        string owner = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Application/Features/Content/Layout/PosterContentSession.cs"));
        Assert.Contains("ContentHeatSession", owner);
        Assert.Contains("PosterContentWork", owner);
        foreach (string forbidden in new[] { "DispatcherQueue", "Sqlite", "PosterVisualWindow", "System.Timers" })
            Assert.DoesNotContain(forbidden, owner);
        Assert.Contains("published = ApplyContentFrame(_bounds)", host);
        Assert.Contains("CompleteHeatPublication(published)", host);
        Assert.DoesNotContain("ContentLayoutProposals.ResolveHeat", host);
        Assert.DoesNotContain("ContentHeatProposalBuffer", host);
    }

    [Fact]
    public void BehaviorModelsUseTheSharedProposalAndAccessibilityBoundaries()
    {
        string root = FindRepositoryRoot();
        string producer = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Application/Features/Content/Layout/ContentActivityProducer.cs"));
        Assert.Contains("registry.EvaluateAsync", producer);
        Assert.DoesNotContain("ContentActivityPolicy.Evaluate", producer);
        string registry = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Application/Features/Content/Layout/ContentModelRegistry.cs"));
        foreach (string forbidden in new[] { "Sqlite", "DispatcherQueue", "LoadFrom", "Assembly.Load" }) Assert.DoesNotContain(forbidden, registry);
        string transition = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.ModelTransition.cs"));
        Assert.Contains("CompositionTarget.Rendering -=", transition);
        Assert.Contains("CanAnimateContent", transition);
        Assert.DoesNotContain("CreateTimer", transition);
        Assert.DoesNotContain("ContentLayoutSolver", transition);
    }

    [Fact]
    public void FileActionVerticalSliceUsesApplicationPortsAndBootstrapComposition()
    {
        string root = FindRepositoryRoot();
        var surfaces = string.Join("\n", new[] { "Views/Poster/PosterVisualWindow.FileActions.cs",
            "Views/Poster/PosterVisualWindow.NewContent.cs", "Views/Poster/PosterVisualWindow.InlineRename.cs",
            "Runtime/Posters/PosterSpatialRuntime.FileActions.cs" }.Select(name =>
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
        var surfaces = string.Join("\n", new[] { "Runtime/Posters/PosterSpatialRuntime.Incoming.cs", "Runtime/Posters/PosterSpatialRuntime.NativeExit.cs",
            "Runtime/Posters/PosterSpatialRuntime.TransferUseCases.cs", "Views/Poster/PosterVisualWindow.Transfers.cs" }.Select(name =>
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
    public void MappingObservationAndStartupAreComposedBehindApplicationBoundaries()
    {
        string root = FindRepositoryRoot();
        foreach (string name in new[] { "Features/Content/App.Transfers.cs", "Runtime/Posters/PosterSpatialRuntime.MappedPosterCreation.cs", "Runtime/Posters/PosterSpatialRuntime.MappingStatus.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", name));
            foreach (string forbidden in new[] { "DesktopUp.Infrastructure", "Sqlite", "LocalTransferFiles", "_transferStore" })
                Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
        string monitor = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Infrastructure/ContentObservation/ManagedContentMonitor.cs"));
        Assert.Contains("IContentObservation", monitor);
        Assert.DoesNotContain("Sqlite", monitor);
        Assert.DoesNotContain("MappedDirectoryService", monitor);
        Assert.DoesNotContain("ManagedContentSynchronizer", monitor);
        string composition = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Bootstrap/App.ContentObservation.cs"));
        foreach (string type in new[] { "ContentStartupUseCase", "ContentObservationService", "ManagedContentSyncUseCase", "MappedDirectoryUseCase" })
            Assert.Contains(type, composition);
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
        string source = File.ReadAllText(Path.Combine(root, "src", "DesktopUp.App", "Lifecycle", "TrayService.cs"));
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
                "Views/Poster/PosterVisualWindow.cs",
                "Views/ControlCenter/ControlCenterService.cs",
                "Views/ControlCenter/ControlCenterWindow.xaml.cs",
                "Views/ControlCenter/ControlCenterWindow.AutoPosture.cs",
                "Lifecycle/TrayService.cs"
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
        Assert.Contains("guardPosterWindow: false", File.ReadAllText(Path.Combine(appDirectory, "Runtime/Desktop/App.DesktopEnvironment.cs")), StringComparison.Ordinal);
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
            "Views/Poster/PosterVisualWindow.cs"));
        string watcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "Desktop/DesktopEnvironmentWatcher.cs"));

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
            "Views/Poster/PosterVisualWindow.cs"));
        string watcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "Desktop/DesktopEnvironmentWatcher.cs"));
        string attachment = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DesktopUp.Windows",
            "Desktop/DesktopLayerAttachment.cs"));

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
        string backdrop = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.Backdrop.cs"));
        string visual = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.cs"));
        string presentation = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.Presentation.cs"));
        Assert.Contains("new WinUIEx.TransparentTintBackdrop(color)", backdrop);
        Assert.DoesNotContain("static WinUIEx.TransparentTintBackdrop", backdrop);
        Assert.Contains("_windowBackdrop.TintColor = color", backdrop);
        Assert.Contains("CurrentSceneParameters.Surface", backdrop);
        Assert.Contains("ColorBrush(paint.Background, paint.BackgroundAlpha)", backdrop);
        string paint = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Application/Features/Scenes/ScenePresentationAdapter.cs"));
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
            "Views/Poster/PosterVisualWindow.SceneHost.cs", "Views/Poster/PosterVisualWindow.SurfacePaint.cs",
            "Runtime/Posters/PosterSpatialRuntime.SceneMotion.cs", "Views/Poster/PosterVisualWindow.Backdrop.cs"
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
        string backdrop = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.Backdrop.cs"));
        string visual = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/Poster/PosterVisualWindow.cs"));
        string watcher = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Windows/Desktop/DesktopEnvironmentWatcher.cs"));
        string session = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Runtime/Posters/WinUiPosterSession.cs"));
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
        var xaml = System.Xml.Linq.XDocument.Load(Path.Combine(root, "src/DesktopUp.App/Views/ControlCenter/ControlCenterWindow.xaml"));
        var controls = xaml.Descendants().ToArray();
        Assert.DoesNotContain(controls, e => e.Attributes().Any(a => a.Value == "AddReferences_Click" || a.Value == "AddContentReferences"));
        Assert.Single(controls.Where(e => e.Attributes().Any(a => a.Value == "ContentManageButton")));
        Assert.DoesNotContain(controls, e => e.Attributes().Any(a => a.Value == "RepairReference_Click"));
        string menu = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/ControlCenter/ControlCenterWindow.UiCWorkspace.cs"));
        Assert.Contains("ContentKeyboardPad.ContextFlyout?.ShowAt(ContentManageButton)", menu);
        Assert.Contains("ForSameItem(() => _ = RepairSelectedReference())", menu);
        Assert.Contains("view?.SelectedIds.Count == 1", menu);
        Assert.DoesNotContain("全选引用", menu);
        Assert.DoesNotContain("从此海报移除", menu);
        Assert.Single(controls.Where(e => e.Attributes().Any(a => a.Value == "ManagedStorage_Click")));
        Assert.DoesNotContain(controls, e => e.Attributes().Any(a => a.Value == "内容介绍文字"));
    }

    [Fact]
    public void ReturnAndUninstallHostsUseApplicationUseCasesWithoutStorageOrFileWorkflow()
    {
        string root = FindRepositoryRoot();
        foreach (string name in new[] { "Features/Content/App.Uninstall.cs", "Runtime/Posters/PosterSpatialRuntime.ContentReturn.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", name));
            foreach (string forbidden in new[] { "DesktopUp.Infrastructure", "Sqlite", "_transferStore",
                "new PosterContentReturn", "FileStream", "Directory.", "File.Exists" })
                Assert.DoesNotContain(forbidden, source);
        }
        string center = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/ControlCenter/ControlCenterService.cs"));
        Assert.DoesNotContain("new DesktopUp.Infrastructure.PosterContentReturn", center);
        Assert.Contains("Task.Run(contentReturns.Reconcile)", center);
        string bootstrap = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Bootstrap/App.ContentLifecycle.cs"));
        Assert.Contains("ContentReturnUseCase", bootstrap);
        Assert.Contains("UninstallContentUseCase", bootstrap);
        Assert.Contains("LocalUninstallProfile", bootstrap);
    }

    [Fact]
    public void StorageLocationHostUsesOneApplicationWorkflowWithoutLegacyFacade()
    {
        string root = FindRepositoryRoot();
        foreach (string name in new[] { "Views/ControlCenter/ControlCenterService.cs", "Views/ControlCenter/ControlCenterWindow.Transfers.cs" })
        {
            string host = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", name));
            Assert.DoesNotContain("DesktopUp.Infrastructure", host);
            Assert.DoesNotContain("Sqlite", host);
            Assert.Contains("StorageLocationUseCase", host);
        }
        Assert.False(File.Exists(Path.Combine(root, "src/DesktopUp.Infrastructure/ManagedStorageService.cs")));
        string files = File.ReadAllText(Path.Combine(root, "src/DesktopUp.Infrastructure/StorageLocation/StorageRelocationFiles.cs"));
        Assert.DoesNotContain("StorageRelocationResult Execute(", files);
        string bootstrap = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Bootstrap/App.ContentObservation.cs"));
        Assert.Contains("new StorageLocationUseCase", bootstrap);
        Assert.Contains("new LocalStorageRelocationExecution", bootstrap);
        Assert.Contains("SqliteContentStartupSteps(store, _storageLocation", bootstrap);
    }

    [Fact]
    public void ConfigurationViewsUseApplicationPortsAndRetireTheOldController()
    {
        string root = FindRepositoryRoot();
        foreach (string name in new[] { "Features/Configuration/App.Portable.cs", "Views/Recovery/StateRecoveryWindow.cs",
            "Views/ControlCenter/ControlCenterWindow.ContentIcons.cs", "Views/ControlCenter/ControlCenterWindow.Portable.cs", "Views/ControlCenter/ControlCenterService.cs" })
        {
            string host = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App", name));
            Assert.DoesNotContain("DesktopUp.Infrastructure", host);
            Assert.DoesNotContain("LocalContentReferenceProbe", host);
            Assert.DoesNotContain("is StateProjectionCoordinator", host);
        }
        Assert.False(File.Exists(Path.Combine(root, "src/DesktopUp.App/PortableProfileController.cs")));
        Assert.Equal(typeof(IStateCommandService).Assembly, typeof(PortableProfileUseCase).Assembly);
        Assert.Equal(typeof(IStateCommandService).Assembly, typeof(IStateBackupRecovery).Assembly);
        Assert.IsAssignableFrom<IStateBackupRecovery>(new StateBackupRecovery(
            new UserDataPaths(Path.Combine(Path.GetTempPath(), "architecture-unused")), () => false));
    }

    [Fact]
    public void ReferenceRepairAndProductionTransfersDoNotRetainStorageOrRetiredExecutors()
    {
        string root = FindRepositoryRoot();
        string repair = File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/ControlCenter/ControlCenterWindow.References.cs"));
        Assert.Contains("ContentReferenceRepairUseCase", repair);
        Assert.DoesNotContain("DesktopUp.Infrastructure", repair);
        Assert.DoesNotContain("LocalContentReferenceProbe", repair);
        Assert.DoesNotContain("EditReferences(bool", repair);
        Assert.DoesNotContain("confirmConversion", repair);
        // Delete is a live keyboard entry, not dead code merely because it has no menu entry.
        Assert.Contains("RemoveSelectedReference()", repair);
        Assert.Contains("RemoveSelectedReference()", File.ReadAllText(Path.Combine(root, "src/DesktopUp.App/Views/ControlCenter/ControlCenterWindow.ContentLayout.cs")));
        var runtimeSources = Directory.GetFiles(Path.Combine(root, "src/DesktopUp.App/Runtime/Posters"),
            "PosterSpatialRuntime*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(runtimeSources);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "src/DesktopUp.App"), "PosterSpatialRuntime*.cs"));
        foreach (string file in runtimeSources)
        {
            string source = File.ReadAllText(file);
            foreach (string forbidden in new[] { "DesktopUp.Infrastructure", "Sqlite", "_transferStore", "RunTransfer(", "RunOutgoing(" })
                Assert.DoesNotContain(forbidden, source);
        }
        Assert.False(File.Exists(Path.Combine(root, "src/DesktopUp.App/PosterSpatialRuntime.Outgoing.cs")));
        Assert.True(File.Exists(Path.Combine(root, "tools/review/App/PosterSpatialRuntime.LegacyOutgoingProbe.cs")));
    }

    [Fact]
    public void MappedCreationAndLegacyTestAdaptersStayOutsideProductionStorageWorkflow()
    {
        Assert.Equal(typeof(ContentCreationUseCase).Assembly, typeof(MappedContentCreationUseCase).Assembly);
        Assert.DoesNotContain(typeof(IContentCreationStorage).GetMethods(), m => m.Name == "CreateMapped");
        var infrastructure = typeof(SqliteStateStore).Assembly;
        foreach (string retired in new[] { "MappedDirectoryService", "ContentCreationService",
            "ContentRenameService", "ContentDeletionService", "ManagedContentSynchronizer", "PosterContentReturn" })
            Assert.Null(infrastructure.GetType("DesktopUp.Infrastructure." + retired));
        Assert.DoesNotContain(infrastructure.GetTypes(), t => t.Namespace == "DesktopUp.Testing");
        Assert.Null(typeof(CrossPosterTransfer).GetMethod("Execute"));
        Assert.Null(typeof(MappedDirectoryIngress).GetMethod("Execute"));
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
