using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class ModelProtocolReviewFixture
{
    internal static void Create(string requested, bool templateComponents = false)
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
        var poster = PosterConfigurationFactory.NewBasicBox(templateComponents ? "组件 · 独立验收" : "模型 · 独立验收", "DISPLAY1") with
        { PosterId = BuiltInStateIdentities.PrimaryPosterId, WidthDip = 560, ExpandedHeightDip = 650,
            HorizontalAnchor = .08, VerticalAnchor = .1 };
        repository.EnsureInitialized(poster);
        var coordinator = new StateProjectionCoordinator(repository); coordinator.EnsureInitialized();
        string storageRoot = Path.Combine(root, "storage"); Directory.CreateDirectory(storageRoot);
        var provisioner = new ManagedStorageProvisioner(store); var preview = provisioner.Preview(storageRoot);
        var storage = provisioner.Confirm(preview, preview.Fingerprint);
        foreach (string name in new[] { "01-常用笔记.txt", "02-最近打开.txt", "03-参考资料.txt", "04-项目清单.txt", "05-待办事项.txt" })
        {
            var current = repository.Read();
            new ContentCreationFixture(store).Create(poster.PosterId, new(storage, current.ContentVersion,
                current.Catalog.Posters.Single(p => p.Poster.PosterId == poster.PosterId).EntityVersion), NewContentKind.TextFile, name);
        }
        if (templateComponents)
        {
            var basic = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "basic-box");
            int index = 0;
            foreach (var components in new TemplateHeadingComponent[][] {
                [TemplateHeadingComponent.Count, TemplateHeadingComponent.Title], [TemplateHeadingComponent.Subtitle] })
            {
                index++;
                var recipe = basic with
                {
                    Schema = basic.Schema with { SchemaVersion = 2, TemplateId = "review-components-" + index,
                        Name = index == 1 ? "数量在前 · 大标题" : "只装副标题" },
                    Base = basic.Base with { Heading = true }, Components = components
                };
                var current = coordinator.Refresh();
                var result = coordinator.Execute(new RegisterTemplateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
                    current.Revision, StateReadSet.ForPoster(poster.PosterId, current.PostersById[poster.PosterId].EntityVersion) with
                    { SettingsVersion = current.ControlCenter.Settings.EntityVersion }, TemplateRecipeCodec.Serialize(recipe),
                    index == 1 ? poster.PosterId : null));
                if (!result.Succeeded) throw new IOException(result.Error);
            }
        }
        Console.WriteLine(profile);
    }
}
