using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class PortableModuleFixture
{
    public static void Run(string[] args)
    {
        string operation = args[0];
        if (operation is not ("portable-export" or "portable-import" or "portable-check") ||
            args.Length != (operation == "portable-check" ? 2 : 3)) throw new ArgumentException("Invalid portable module fixture arguments.");
        string root = Path.GetFullPath(args[1]);
        string ordinary = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
        if (!Path.IsPathFullyQualified(args[1]) || root.Equals(ordinary, StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith(ordinary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Path.GetPathRoot(root) == root)
            throw new ArgumentException("An explicit isolated fixture root is required.");
        string proof = Path.Combine(root, "r7-portable-expected.json");
        string sourceCopy = Path.Combine(root, "r7-portable-source.json");
        if (operation == "portable-import")
        { if (Directory.Exists(root) || File.Exists(root)) throw new ArgumentException("Portable import fixture requires a NEW root."); }
        else if (!File.Exists(proof) && !File.Exists(Path.Combine(root, "scene-module-expected.json")))
            throw new ArgumentException("Missing isolated fixture receipt.");
        var paths = new UserDataPaths(root); var files = new PortableProfileFile(paths);
        byte[]? source = operation == "portable-import" ? files.Read(args[2]) : null;
        using var store = new SqliteStateStore(paths);
        var initialized = store.Initialize();
        if (!initialized.Succeeded) throw new InvalidOperationException(initialized.Error);
        var repository = new SqliteStateCommandRepository(store);
        if (operation == "portable-export")
        {
            string before = StateRequestFingerprint.Compute(repository.Read());
            files.WriteAtomic(args[2], PortableProfileCodec.Export(repository.Read()), false);
            Require(before == StateRequestFingerprint.Compute(repository.Read()), "export mutated the source");
        }
        else
        {
            var coordinator = new StateProjectionCoordinator(repository); coordinator.BindPortableRuntime(_ => true);
            if (operation == "portable-import")
            {
                // Model an explicitly initialized zero-poster profile, as the actual app does.
                // Raw SQLite initialization alone must not bypass the repository's NotReady guard.
                var seed = PosterConfigurationFactory.NewBasicBox("R7 empty-profile seed", "DISPLAY1");
                coordinator.EnsureInitialized(seed);
                var removal = coordinator.PrepareRemoval(seed.PosterId)!;
                var removed = coordinator.Execute(new RemovePosterCommand("r7-empty-profile", StateCommandSource.ControlCenter,
                    removal.Revision, removal.ReadSet, seed.PosterId, true));
                Require(removed.Succeeded, removed.Error ?? "empty profile initialization failed");
                var topology = new DisplayTopologySnapshot("r7-module", [new("DISPLAY1", true,
                    new(0, 0, 3840, 2160), new(0, 0, 3840, 2160), 96, 96)], 1, DateTimeOffset.UnixEpoch);
                var plan = PortableProfilePlanner.Plan(PortableProfileCodec.Parse(source!), repository.Read(), topology, PortableImportMode.Replace, true);
                var committed = coordinator.ExecutePortable(new("r7-module-import", plan));
                Require(committed.Succeeded, committed.Error ?? "portable import failed");
                files.WriteAtomic(sourceCopy, source!, false);
                File.WriteAllText(proof, StateRequestFingerprint.Compute(repository.Read()));
            }
            Require(File.ReadAllText(proof) == StateRequestFingerprint.Compute(repository.Read()), "restart/module restore mutated imported state");
            var original = PortableProfileCodec.Parse(files.Read(sourceCopy));
            var exported = PortableProfileCodec.Parse(PortableProfileCodec.Export(repository.Read()));
            Require(original.Settings == exported.Settings, "global preferences changed");
            Require(original.Posters.Count == exported.Posters.Count, "poster count changed");
            for (int i = 0; i < original.Posters.Count; i++)
                Require(JsonNode.DeepEquals(Semantic(original.Posters[i]), Semantic(exported.Posters[i])), "poster or template values changed");
            foreach (var recipe in original.TemplateCatalog!.Value.EnumerateArray())
                Require(exported.TemplateCatalog!.Value.EnumerateArray().Any(other =>
                    JsonNode.DeepEquals(Definition(recipe.GetRawText()), Definition(other.GetRawText()))), "catalog entry lost");
            foreach (var poster in coordinator.Refresh().Posters)
            {
                var value = PosterPresentationPolicy.ReadStoredParameters(poster.Poster.LayoutParametersJson, poster.Poster.TemplateKey);
                bool missing = value.RecipeInstances?.Values.Any(i => !SceneCapabilityRegistry.Available(i.Recipe.Schema.RendererCapability)) == true ||
                    poster.Poster.TemplateKey == "lamp" && !SceneCapabilityRegistry.Available(TemplateParameterSchemas.LampCapability);
                Require(poster.Presentation.CanEdit == !missing, "read-only/module restoration boundary mismatch");
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, operation, root,
            lampAvailable = SceneCapabilityRegistry.Available("lamp.paint@1"), rainAvailable = SceneCapabilityRegistry.Available("rain.paint@1"),
            nativeInput = false, realCrossHost = false }));
    }
    private static JsonObject Definition(string json)
    {
        var recipe = TemplateRecipeCodec.Parse(json, stored: true);
        var node = JsonNode.Parse(TemplateRecipeCodec.Serialize(recipe, stored: true))!.AsObject();
        node.Remove("id"); return node;
    }
    private static JsonObject Semantic(PortablePosterProfile poster)
    {
        var node = JsonSerializer.SerializeToNode(poster)!.AsObject();
        foreach (var field in new[] { "Key", "MonitorHint", "HorizontalAnchor", "VerticalAnchor" }) node.Remove(field);
        var parameters = JsonNode.Parse(poster.LayoutParametersJson)!.AsObject();
        JsonNode selected = JsonValue.Create(poster.TemplateKey)!;
        if (parameters["recipes"] is JsonArray recipes)
            foreach (var entry in recipes)
            {
                var definition = entry!["definition"]!;
                var comparable = Definition(definition.ToJsonString());
                if (definition["id"]!.GetValue<string>() == poster.TemplateKey) selected = comparable.DeepClone();
                entry["definition"] = comparable;
            }
        node["TemplateKey"] = selected;
        node["LayoutParameters"] = parameters;
        return node;
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
