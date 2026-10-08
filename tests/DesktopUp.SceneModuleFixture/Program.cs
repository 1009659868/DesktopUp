using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

if (args.Length > 0 && args[0].StartsWith("portable-", StringComparison.Ordinal))
{
    PortableModuleFixture.Run(args); return;
}

if (args.Length is not (2 or 3) || args[0] is not ("seed" or "backup" or "restore" or "check"))
    throw new ArgumentException("Usage: SceneModuleFixture seed|backup|restore|check <isolated-root>");
string moduleId = args.Length == 3 ? args[2].ToLowerInvariant() : "lamp";
if (moduleId is not ("lamp" or "rain")) throw new ArgumentException("Unknown fixture module.");
string capability = moduleId + ".paint@1";
string root = Path.GetFullPath(args[1]);
string normal = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
if (!Path.IsPathFullyQualified(args[1]) || root.Equals(normal, StringComparison.OrdinalIgnoreCase) ||
    root.StartsWith(normal + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Path.GetPathRoot(root) == root)
    throw new ArgumentException("An explicit isolated directory is required.");
var paths = new UserDataPaths(root);
string expectedPath = Path.Combine(root, "scene-module-expected.json");
if (args[0] == "restore")
{
    var name = File.ReadAllText(Path.Combine(root, "scene-module-backup.txt"));
    var restored = new StateBackupRecovery(paths, () => true).Restore(name);
    if (!restored.Succeeded) throw new InvalidOperationException(restored.Error);
}
if (args[0] == "seed" && Directory.Exists(root)) throw new ArgumentException("Seed requires a NEW directory.");
if (args[0] != "seed" && !File.Exists(expectedPath)) throw new ArgumentException("Missing isolated fixture receipt.");
using var store = new SqliteStateStore(paths);
var initialized = store.Initialize();
if (!initialized.Succeeded) throw new InvalidOperationException(initialized.Error);
var repository = new SqliteStateCommandRepository(store);
var coordinator = new StateProjectionCoordinator(repository);
if (args[0] == "seed")
{
    if (!SceneCapabilityRegistry.Available(capability)) throw new InvalidOperationException("Seed requires the module.");
    var builtIn = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == moduleId);
    var recipe = builtIn with { Schema = builtIn.Schema with { TemplateId = "module-custom", Name = "Module custom" } };
    var value = PosterPresentationPolicy.Defaults() with { Lamp = new("#C1926A", 17.25, 83, "#1F3038", "#354659", 25, true, false, 24, 31) };
    if (moduleId == "rain") value = TemplateRecipeInstancePolicy.Select(value, builtIn);
    var seed = new PosterDefinition(BuiltInStateIdentities.PrimaryPosterId, "A / module " + moduleId, "Module preservation", "DISPLAY1",
        0, 0, 300, 600, .05, .05, .15, .5, false, false, false, "adaptive-grid", PosterPresentationPolicy.Serialize(value), moduleId);
    var current = coordinator.EnsureInitialized(seed);
    var registered = coordinator.Execute(new RegisterTemplateCommand("module-register", StateCommandSource.ControlCenter,
        current.Revision, StateReadSet.ForSettings(current.ControlCenter.Settings.EntityVersion), TemplateRecipeCodec.Serialize(recipe)));
    if (!registered.Succeeded) throw new InvalidOperationException(registered.Error);
    current = registered.Projection!;
    var custom = TemplateRecipeInstancePolicy.Select(value, recipe);
    custom = TemplateRecipeInstancePolicy.UpdateValues(custom, recipe.Id, TemplateRecipeInstancePolicy.CurrentValues(custom, recipe.Id)
        .Set(moduleId == "lamp" ? "lamp.intensity" : "rain.count", TemplateParameterValue.FromNumber(moduleId == "lamp" ? 42.5 : 63)).Set(moduleId == "lamp" ? "surface.opacity" : "cloud.wind", TemplateParameterValue.FromNumber(63)));
    foreach (var (id, json, x) in new[] { (recipe.Id, PosterPresentationPolicy.Serialize(custom), .43),
        ("basic-box", PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults("basic-box")), .78) })
    {
        var result = coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
            current.Revision, StateReadSet.ForCatalog(current.CatalogVersion),
            seed with { Title = id, TemplateKey = id, LayoutParametersJson = json, HorizontalAnchor = x }));
        if (!result.Succeeded) throw new InvalidOperationException(result.Error);
        current = result.Projection!;
    }
    File.WriteAllText(expectedPath, Snapshot(repository.Read()));
}
string actual = Snapshot(repository.Read());
if (actual != File.ReadAllText(expectedPath)) throw new InvalidOperationException("Template IDs/raw JSON/catalog changed.");
string? backup = null;
if (args[0] == "backup")
{
    var result = store.CreateBackup("scene-module-removed");
    if (!result.Succeeded) throw new InvalidOperationException(result.Error);
    backup = result.FileName!;
    if (!store.ValidateBackup(Path.Combine(paths.Backups, backup)).IsValid) throw new InvalidOperationException("Backup validation failed.");
    File.WriteAllText(Path.Combine(root, "scene-module-backup.txt"), backup);
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, mode = args[0], root, backup,
    lampAvailable = SceneCapabilityRegistry.Available(TemplateParameterSchemas.LampCapability),
    preserved = "TemplateKey + exact LayoutParametersJson + ContentLayoutJson + exact catalog",
    posters = coordinator.Refresh().Posters.Select(p => new { p.Poster.TemplateKey, p.Presentation.CanEdit, p.Presentation.Warning }) }));

string Snapshot(DesktopStateSnapshot state) => JsonSerializer.Serialize(new
{
    capability,
    catalog = state.Settings.Settings.TemplateCatalogJson,
    entries = state.Catalog.Posters.OrderBy(p => p.Poster.PosterId, StringComparer.Ordinal).Select(p =>
        new { id = p.Poster.PosterId, template = p.Poster.TemplateKey, parameters = p.Poster.LayoutParametersJson, content = p.Poster.ContentLayoutJson })
});
