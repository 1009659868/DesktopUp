using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using System.Text.Json;

if (args.Length != 2 || !int.TryParse(args[1], out int count) || count is < 0 or > 3)
    throw new ArgumentException("Usage: DesktopUp.SessionFixture <NEW isolated directory> <0..3>");
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
DesktopUpProjection current = coordinator.EnsureInitialized(seed);
for (int index = 1; index < count; index++)
{
    var result = coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
        current.Revision, StateReadSet.ForCatalog(current.CatalogVersion), seed with
        {
            Title = $"{(char)('A' + index)} / Session fixture", HorizontalAnchor = index * 0.38,
            IsLocked = index == 1, IsCollapsed = index == 2
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
