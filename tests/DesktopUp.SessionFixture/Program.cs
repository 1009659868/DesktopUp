using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using System.Text.Json;
using DesktopUp.Testing;

if (args.Length == 2 && args[1] == "--joint-model-cost")
{
    await JointModelCostFixture.Run(args[0]);
    return;
}

if (args.Length == 2 && args[1] is "--model-protocol" or "--template-components")
{
    ModelProtocolReviewFixture.Create(args[0], templateComponents: args[1] == "--template-components");
    return;
}

if (args.Length == 2 && args[1] == "--architecture-attribution")
{
    ReadValidationCostFixture.Run(args[0]);
    return;
}

if (args.Length == 2 && args[1] == "--architecture-directory-cost")
{
    await DirectoryObservationCostFixture.Run(args[0]);
    return;
}

if (args.Length == 2 && args[1] is "--architecture-cost" or "--architecture-discovery-cost")
{
    await ReadObservationCostFixture.Run(args[0], discovery: args[1] == "--architecture-discovery-cost");
    return;
}

// Read-only validation shares the production parser; no fixture/database is created in this mode.
if (args.Length == 2 && args[1] == "--content-compatibility")
{
    ContentCompatibilityReviewFixture.Create(args[0]);
    return;
}
if (args.Length == 2 && args[1] == "--uninstall-content")
{
    UninstallContentFixture.Create(args[0]);
    return;
}
if (args.Length == 2 && args[1] == "--architecture-references")
{
    ReferenceRepairReviewFixture.Create(args[0]);
    return;
}
if (args.Length == 2 && args[1] == "--architecture-icons")
{
    UninstallContentFixture.Create(args[0], title: "图标 · 自建样例");
    return;
}
if (args.Length == 2 && args[1] == "--architecture-storage")
{
    UninstallContentFixture.Create(args[0], title: "收纳位置 · 自建样例");
    return;
}
if (args.Length == 2 && args[1] == "--uninstall-content-review")
{
    ContentReturnReviewFixture.Create(args[0], uninstall: true);
    return;
}
if (args.Length == 2 && args[1] == "--architecture-lifecycle")
{
    ContentReturnReviewFixture.Create(args[0], architecture: true);
    return;
}
if (args.Length == 2 && args[1] == "--content-return")
{
    ContentReturnReviewFixture.Create(args[0]);
    return;
}
if (args.Length == 2 && args[1] == "--cross-poster")
{
    CrossPosterReviewFixture.Create(args[0]);
    return;
}
if (args.Length == 2 && args[0] == "--validate-animation-config")
{
    try { Console.WriteLine(JsonSerializer.Serialize(PosterAnimationConfiguration.Read(Path.GetFullPath(args[1]), true))); }
    catch (Exception exception) { Console.Error.WriteLine(exception.Message); Environment.ExitCode = 2; }
    return;
}

if (args.Length == 3 && args[1] == "--content-layout")
{
    CreateContentLayoutFixture(args[0], args[2]);
    return;
}

if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out int count) || count is < 0 or > 6 ||
    args.Length == 3 && args[2] is not ("--measure-expanded" or "--measure-collapsed" or "--measure-hidden" or "--multi-instance" or "--spatial-chain" or "--spatial-chain-r2" or "--compact-pair" or "--r2-up" or "--r2-mixed" or "--r2-constrained" or "--r2-blocked"))
    throw new ArgumentException("Usage: DesktopUp.SessionFixture <NEW isolated directory> <0..3> [--multi-instance|--spatial-chain|--compact-pair|--r2-...] or <1|3|6> --measure-expanded|collapsed|hidden");
string? measurement = args.Length == 3 && args[2].StartsWith("--measure-", StringComparison.Ordinal) ? args[2][10..] : null;
if (measurement is null && count > 3 || measurement is not null && count is not (1 or 3 or 6))
    throw new ArgumentException("Only measurement fixtures accept 1/3/6; existing modes remain 0..3.");
bool spatialChain = args.Length == 3 && args[2] is "--spatial-chain" or "--spatial-chain-r2";
bool multiInstance = args.Length == 3 && args[2] == "--multi-instance";
if (multiInstance && count != 3) throw new ArgumentException("--multi-instance requires exactly three posters.");
bool r2Chain = args.Length == 3 && args[2] == "--spatial-chain-r2";
bool compactPair = args.Length == 3 && args[2] == "--compact-pair";
string? r2 = args.Length == 3 && args[2].StartsWith("--r2-", StringComparison.Ordinal) ? args[2][5..] : null;
if (r2 is not null && count != (r2 == "up" ? 2 : 3)) throw new ArgumentException("R2 up requires 2 posters; other R2 fixtures require 3.");
if (compactPair && count != 2) throw new ArgumentException("--compact-pair requires exactly two posters.");
string root = Path.GetFullPath(args[0]);
string userRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
if (Directory.Exists(root) || File.Exists(root) || root.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Fixture requires a new isolated directory outside the normal user profile.");
var paths = new UserDataPaths(root);
using var store = new SqliteStateStore(paths);
var initialized = store.Initialize();
if (!initialized.Succeeded) throw new InvalidOperationException(initialized.Error);
var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(store));
var seed = new PosterDefinition(BuiltInStateIdentities.PrimaryPosterId, "A / Session fixture", "M03 isolated configuration", "DISPLAY1",
    0, 0, 300, 480, 0.05, 0.1, 0.15, 0.45, false, false, false, "adaptive-grid", "{}", "night-ops");
if (spatialChain) seed = seed with { WidthDip = 240, IsCollapsed = true, HorizontalAnchor = 0.05, VerticalAnchor = 0.01 };
if (multiInstance) seed = MultiPosterFixtureData.Create(0);
if (measurement is not null) seed = MeasurementFixtureData.Create(0, count, measurement);
if (compactPair) seed = seed with { WidthDip = 240, IsCollapsed = true, HorizontalAnchor = 0.05, VerticalAnchor = 0.08 };
// Isolated R2 native fixtures: current verified primary work area 2560x1392 at 96 DPI.
// Pure DPI fixtures test other scales independently; these are not cross-device profile presets.
if (r2 is not null) seed = seed with { Title = "A / R2 " + r2, WidthDip = 300, ExpandedHeightDip = 600,
    IsCollapsed = true, HorizontalAnchor = 0.10, VerticalAnchor = 800 / 1332d, NormalizedExpandedHeight = 600 / 1392d };
DesktopUpProjection current = coordinator.EnsureInitialized(seed);
for (int index = 1; index < count; index++)
{
    var result = coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
        current.Revision, StateReadSet.ForCatalog(current.CatalogVersion), measurement is not null ? MeasurementFixtureData.Create(index, count, measurement) : multiInstance ? MultiPosterFixtureData.Create(index) : r2 is not null ? seed with
        {
            Title = index == 1 ? "B / R2 bottom obstacle" : "U / R2 upper obstacle",
            ExpandedHeightDip = index == 1 ? 400 : r2 == "mixed" ? 392 : r2 == "constrained" ? 542 : 700,
            VerticalAnchor = index == 1 ? 1 : 0, IsLocked = true, IsCollapsed = false
        } : seed with
        {
            Title = $"{(char)('A' + index)} / Session fixture", HorizontalAnchor = spatialChain || compactPair ? 0.05 : index * 0.38,
            VerticalAnchor = compactPair ? 0.01 : spatialChain ? (index == 1 ? 0.08 : r2Chain ? 0.48 : 0.32) : seed.VerticalAnchor,
            ExpandedHeightDip = spatialChain ? (r2Chain ? 360 : 240) : seed.ExpandedHeightDip,
            IsLocked = !spatialChain && !compactPair && index == 1, IsCollapsed = compactPair || !spatialChain && index == 2
        }));
    if (!result.Succeeded) throw new InvalidOperationException(result.Error);
    current = result.Projection!;
}
if (count == 0)
{
    var preview = coordinator.PrepareRemoval(seed.PosterId)!;
    var result = coordinator.Execute(new RemovePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
        preview.Revision, preview.ReadSet, seed.PosterId, true));
    if (!result.Succeeded) throw new InvalidOperationException(result.Error);
    current = result.Projection!;
}
Console.WriteLine(JsonSerializer.Serialize(new { root, revision = current.Revision, posterIds = current.Posters.Select(p => p.Poster.PosterId) }));

// Content layouts are a separate opt-in mode; historical numeric scenarios above remain unchanged.
static void CreateContentLayoutFixture(string requestedRoot, string scenario)
{
    string[] allowed = ["equal", "illustration", "covers", "landscape", "three", "empty", "hundred", "thousand", "rejected-heat", "resource-one", "resource-three", "resource-six"];
    if (!allowed.Contains(scenario, StringComparer.Ordinal))
        throw new ArgumentException("Unknown content layout fixture. Use equal|illustration|covers|landscape|three|empty|hundred|thousand|rejected-heat|resource-one|resource-three|resource-six.");
    if (!Path.IsPathFullyQualified(requestedRoot))
        throw new ArgumentException("Content layout fixture requires an absolute new isolated directory.");
    string contentRoot = Path.GetFullPath(requestedRoot);
    string ordinaryRoot = Path.GetFullPath(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3")).TrimEnd(Path.DirectorySeparatorChar);
    if (Directory.Exists(contentRoot) || File.Exists(contentRoot) ||
        contentRoot.Equals(ordinaryRoot, StringComparison.OrdinalIgnoreCase) ||
        contentRoot.StartsWith(ordinaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        Path.GetPathRoot(contentRoot) == contentRoot)
        throw new ArgumentException("Content fixture requires a NEW isolated directory outside the normal profile; existing data is preserved.");

    int resourceCount = scenario switch { "resource-one" => 1, "resource-three" => 3, "resource-six" => 6, _ => 0 };
    string[] examples = scenario == "three" ? ["illustration", "covers", "landscape"] :
        resourceCount > 0 ? Enumerable.Repeat("equal", resourceCount).ToArray() : [scenario];
    // Same native baseline as the existing measurement fixtures, not a cross-host layout assertion.
    // At a 1920+ DIP work width these three authored boxes do not overlap; smaller hosts should use one example.
    PosterDefinition Definition(int index)
    {
        string example = examples[index];
        string id = index == 0 ? BuiltInStateIdentities.PrimaryPosterId : "content-fixture-" + index;
        ContentLayoutDocument document = example switch
        {
            "illustration" => ContentLayoutSamples.Illustration(id),
            "covers" => ContentLayoutSamples.Covers(id),
            "landscape" => ContentLayoutSamples.Landscape(id),
            "empty" => ContentLayoutSamples.Equal(id, 0),
            "hundred" => ContentLayoutSamples.Equal(id, 100),
            "thousand" => ContentLayoutSamples.Equal(id, 1000),
            // The author may legally move its 4x4 hard rectangle to (0,0), but a 1.5 heat
            // projection makes that destination 6x6 and intersects the preserved keep-out area.
            "rejected-heat" => new(id, [new(id + ":rejected-heat", 0, 4, 4)
                { Column=0, Row=8, FixedPosition=true }])
            {
                SchemaVersion=2, AllowHeatDrivenSize=true,
                ReservedSpaces=[new(4,0,4,4) { Kind=ContentReservedKind.Explicit, Name="rejected heat destination" }]
            },
            _ => ContentLayoutSamples.Equal(id)
        };
        ContentLayoutCommandPolicy.ValidateAuthoring(document);
        string template = example == "covers" ? "night-ops" : "basic-box";
        (double width, double height) = example switch
        {
            // The combined gate replaces all three samples on A, including at 1.2x metrics.
            // Fit the largest full cross-axis intent; never rely on the retired clipping behavior.
            "illustration" => scenario == "three" ? (800, 960) : (540, 1020),
            "covers" => scenario == "three" ? (620, 840) : (540, 880),
            "landscape" => scenario == "three" ? (740, 540) : (660, 590),
            _ => resourceCount > 0 ? (300, 360) : (540, 720)
        };
        double anchor = scenario == "three" ? index * .5 : .08;
        var definition = PosterConfigurationFactory.NewBasicBox($"{(char)('A' + index)} / L {example}", "DISPLAY1") with
        {
            PosterId = id, ThemeInstanceId = "theme-" + id,
            Subtitle = $"L: {document.Placements.Count} DEMO ITEMS; no files collected",
            WidthDip = width, ExpandedHeightDip = height,
            FallbackXDip = anchor * (2560 - width), FallbackYDip = 24,
            HorizontalAnchor = anchor, VerticalAnchor = .02,
            NormalizedWidth = width / 2560, NormalizedExpandedHeight = height / 1392,
            IsCollapsed = false, IsLocked = false, AutoCollapseEnabled = false, IsVisible = true,
            TemplateKey = template, LayoutParametersJson = PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults(template)),
            ContentLayoutJson = ContentLayoutPolicy.Serialize(document)
        };
        if (resourceCount > 0)
        {
            // Exactly the same per-poster workload at 1/3/6; only identities/count/placement vary.
            // Copy geometry only: the legacy measurement's varied themes are NOT imported.
            var measured = MeasurementFixtureData.Create(index, resourceCount, "expanded");
            definition = definition with { WidthDip=measured.WidthDip, ExpandedHeightDip=measured.ExpandedHeightDip,
                HorizontalAnchor=measured.HorizontalAnchor, VerticalAnchor=measured.VerticalAnchor,
                NormalizedWidth=measured.NormalizedWidth, NormalizedExpandedHeight=measured.NormalizedExpandedHeight,
                FallbackXDip=measured.HorizontalAnchor*(2560-measured.WidthDip), FallbackYDip=measured.VerticalAnchor*(1392-measured.ExpandedHeightDip) };
        }
        return definition;
    }

    PosterDefinition[] definitions = Enumerable.Range(0, examples.Length).Select(Definition).ToArray();
    var contentPaths = new UserDataPaths(contentRoot);
    using var contentStore = new SqliteStateStore(contentPaths);
    var initialization = contentStore.Initialize();
    if (!initialization.Succeeded) throw new InvalidOperationException(initialization.Error);
    var contentCoordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(contentStore));
    DesktopUpProjection projection = contentCoordinator.EnsureInitialized(definitions[0]);
    for (int index = 1; index < definitions.Length; index++)
    {
        var created = contentCoordinator.Execute(new CreatePosterCommand(
            "content-fixture-create-" + index, StateCommandSource.ControlCenter, projection.Revision,
            StateReadSet.ForCatalog(projection.CatalogVersion), definitions[index]));
        if (!created.Succeeded) throw new InvalidOperationException(created.Error);
        projection = created.Projection!;
    }
    var contents = projection.Posters.Select(entry =>
    {
        if (entry.Poster.ContentLayoutJson is null ||
            !ContentLayoutPolicy.TryRead(entry.Poster.ContentLayoutJson, entry.Poster.PosterId, out var document, out var error))
            throw new InvalidOperationException("Fixture content was not persisted with its allocated owner identity.");
        return new { entry.Poster.PosterId, entry.Poster.Title, items = document!.Placements.Count,
            document.Mode, document.ShowBaselineGrid, document.ShowSpanLabels };
    }).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        root = contentRoot, scenario, revision = projection.Revision, posterIds = projection.Posters.Select(p => p.Poster.PosterId),
        contents, demoOnly = true, requiresNewRoot = true
    }));
}
