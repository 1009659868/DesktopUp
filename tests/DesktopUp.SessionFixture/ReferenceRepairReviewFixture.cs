using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using System.Security.Cryptography;
using System.Text.Json;

internal static class ReferenceRepairReviewFixture
{
    internal static void Create(string requested)
    {
        string profile = Path.GetFullPath(requested), root = Path.GetDirectoryName(profile)!;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
            repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository required.");
        if (!profile.StartsWith(Path.Combine(repo, "out", "drop-d3") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(profile) != "profile" || Directory.Exists(profile) ||
            !File.Exists(Path.Combine(root, ".reference-repair-fixture")))
            throw new IOException("New owned reference fixture required.");
        using var store = new SqliteStateStore(new UserDataPaths(profile));
        var initialized = store.Initialize();
        if (!initialized.Succeeded) throw new IOException(initialized.Error);
        var coordinator = new StateProjectionCoordinator(new SqliteStateCommandRepository(store));
        var state = coordinator.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("R1 · 来源修复", "DISPLAY1") with
            { PosterId = BuiltInStateIdentities.PrimaryPosterId });
        string samples = Path.Combine(root, "samples");
        Directory.CreateDirectory(samples);
        var paths = new[] { "original.txt", "replacement.txt", "untouched.txt" }.Select(name => Path.Combine(samples, name)).ToArray();
        foreach (string path in paths)
        {
            using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew));
            writer.Write("Owned R1 reference sample: " + Path.GetFileName(path));
        }
        var command = new ContentReferenceCommand("fixture-references", StateCommandSource.ControlCenter, state.Revision,
            StateReadSet.ForPoster(BuiltInStateIdentities.PrimaryPosterId, state.Posters.Single().EntityVersion) with
                { ContentVersion = state.ContentVersion }, BuiltInStateIdentities.PrimaryPosterId,
            new[] { paths[0], paths[2] }.Select(path =>
                new ContentReferenceEntry(LocalContentReferenceProbe.Inspect(path).Item!, ContentReferenceChoice.CreateIndependent)).ToArray(), true);
        var result = coordinator.Execute(command);
        if (!result.Succeeded) throw new IOException(result.Error);
        File.WriteAllText(Path.Combine(root, "sample-manifest.json"), JsonSerializer.Serialize(
            paths.Select(path => new { Path = path, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) })));
    }
}
