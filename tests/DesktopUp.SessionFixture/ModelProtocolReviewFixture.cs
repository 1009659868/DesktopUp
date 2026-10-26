using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class ModelProtocolReviewFixture
{
    internal static void Create(string requested)
    {
        string profile = Path.GetFullPath(requested), root = Path.GetDirectoryName(profile)!;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "DesktopUp.sln"))) repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
        if (!string.Equals(Path.GetDirectoryName(root), Path.Combine(repo, "out", "drop-d3"), StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(profile) != "profile" || Directory.Exists(profile) ||
            File.ReadAllText(Path.Combine(root, ".model-protocol-fixture")) != "owned")
            throw new IOException("New owned model fixture required.");
        using var store = new SqliteStateStore(new UserDataPaths(profile));
        var init = store.Initialize(); if (!init.Succeeded) throw new IOException(init.Error);
        var repository = new SqliteStateCommandRepository(store);
        var poster = PosterConfigurationFactory.NewBasicBox("模型 · 独立验收", "DISPLAY1") with
        { PosterId = BuiltInStateIdentities.PrimaryPosterId, WidthDip = 560, ExpandedHeightDip = 650,
            HorizontalAnchor = .08, VerticalAnchor = .1 };
        repository.EnsureInitialized(poster);
        string storageRoot = Path.Combine(root, "storage"); Directory.CreateDirectory(storageRoot);
        var provisioner = new ManagedStorageProvisioner(store); var preview = provisioner.Preview(storageRoot);
        var storage = provisioner.Confirm(preview, preview.Fingerprint);
        foreach (string name in new[] { "01-常用笔记.txt", "02-最近打开.txt", "03-参考资料.txt", "04-项目清单.txt", "05-待办事项.txt" })
        {
            var current = repository.Read();
            new ContentCreationFixture(store).Create(poster.PosterId, new(storage, current.ContentVersion,
                current.Catalog.Posters.Single(p => p.Poster.PosterId == poster.PosterId).EntityVersion), NewContentKind.TextFile, name);
        }
        Console.WriteLine(profile);
    }
}
