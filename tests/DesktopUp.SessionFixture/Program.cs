using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using System.Text.Json;
using DesktopUp.Testing;

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
