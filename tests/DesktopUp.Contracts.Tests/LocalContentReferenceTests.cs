using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class LocalContentReferenceTests
{
    [Theory]
    [InlineData("relative.txt")] [InlineData("https://example.com")] [InlineData("shell:AppsFolder")]
    [InlineData(@"\\server\share\file.txt")] [InlineData(@"\\?\C:\file.txt")]
    [InlineData(@"C:\file.txt:stream")] [InlineData("C:\\bad\nname")]
    public void UnsupportedInputDoesNotBecomeFileIdentity(string input) => Assert.Null(LocalContentReferenceProbe.Inspect(input).Item);

    [Theory]
    [InlineData("note.txt", ContentKind.File)] [InlineData("launch.lnk", ContentKind.Shortcut)]
    [InlineData("url.url", ContentKind.Shortcut)] [InlineData("program.exe", ContentKind.File)]
    public void FileProbePreservesShortcutAndExecutableWithoutExecuting(string name, ContentKind kind)
    {
        using var f = new Files(); string file = Path.Combine(f.Root, name);
        File.WriteAllText(file, "sentinel, not executable"); byte[] original = File.ReadAllBytes(file);
        var p = LocalContentReferenceProbe.Inspect(file);
        Assert.NotNull(p.Item); Assert.Equal(kind, p.Item.Kind); Assert.Equal(file, p.Item.Target.Locator);
        Assert.True(LocalContentReferenceProbe.IsCurrent(p)); Assert.Equal(original, File.ReadAllBytes(file));
        File.AppendAllText(file, "changed"); Assert.False(LocalContentReferenceProbe.IsCurrent(p));
        File.Delete(file); Assert.False(LocalContentReferenceProbe.IsCurrent(p));
    }

    [Fact] public void FolderIsOneReferenceNotRecursiveEnumeration()
    {
        using var f = new Files(); File.WriteAllText(Path.Combine(f.Root, "untouched.txt"), "source");
        var p = LocalContentReferenceProbe.Inspect(f.Root); Assert.Equal(ContentKind.Folder, p.Item!.Kind);
        Assert.Null(p.Length); Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact] public void MissingLocalFileIsExplicitlySkipped()
    {
        using var f = new Files(); var p = LocalContentReferenceProbe.Inspect(Path.Combine(f.Root, "missing.txt"));
        Assert.Null(p.Item); Assert.Contains("不存在", p.Message);
    }

    [Fact] public void ReferenceViewUsesFormalMetadataAndNeverAddsLayoutMembers()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); var e = ContentReferencePersistenceTests.Entry("real-name.txt");
        f.Commit(f.AddCommand(f.Main, e)); var state = f.Current;
        var source = new ReferenceContentViewSource(state.ContentCatalog, f.Main, state.ContentVersion);
        ContentLayoutPolicy.TryRead(state.PostersById[f.Main].Poster.ContentLayoutJson, f.Main, out var d, out _);
        Assert.Equal("real-name.txt", Assert.Single(ContentSourceProjection.Read(source, d!)).Name);
        Assert.Empty(source.Read(d! with { Placements = [] }));
    }

    [Fact] public void PortableAndConfigurationCloneFailClosedForRealReferences()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); f.Commit(f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry()));
        var state = new SqliteStateCommandRepository(f.Store).Read();
        Assert.Throws<ArgumentException>(() => PortableProfileCodec.Export(state));
        var current = f.Current;
        var result = f.Coordinator.Execute(new ClonePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            current.Revision, StateReadSet.ForPoster(f.Main, current.PostersById[f.Main].EntityVersion) with { CatalogVersion = current.CatalogVersion }, f.Main));
        Assert.False(result.Succeeded); Assert.Single(f.Current.Posters);
    }

    [Fact] public void AppendInManualKeepsExistingPlacementAndAutomaticReturnState()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); f.Commit(f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry()));
        var current = f.Current; var p = current.PostersById[f.Main];
        ContentLayoutPolicy.TryRead(p.Poster.ContentLayoutJson, f.Main, out var old, out _);
        var manual = old! with { Mode = ContentLayoutMode.Manual, SavedAutomaticMode = old.Mode, DocumentVersion = old.DocumentVersion + 1,
            Placements = old.Placements.Select(x => x with { Column = 2, Row = 3, Version = x.Version + 1,
                SavedAutomaticState = new(x.Order, x.ColumnSpan, x.RowSpan, x.ManualSize, x.PinnedIndex) }).ToArray() };
        var result = f.Coordinator.Execute(new ContentLayoutCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            current.Revision, f.Main, StateReadSet.ForPoster(f.Main, p.EntityVersion), old.DocumentVersion, old.ItemSetVersion, manual));
        Assert.True(result.Succeeded, result.Error);
        f.Commit(f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry("new.txt")));
        ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson, f.Main, out var next, out _);
        Assert.Equal(manual.Placements[0], next!.Placements[0]); Assert.NotNull(next.Placements[1].SavedAutomaticState);
        Assert.False(next.Placements[1].FixedPosition); Assert.True(next.Placements[1].Row >= 7);
    }

    [Fact] public void LocalBackupContainsReferencesAndRejectsBrokenMapping()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); f.Commit(f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry()));
        var backup = f.Store.CreateBackup("reference-test"); Assert.True(backup.Succeeded, backup.Error);
        Assert.True(f.Store.ValidateBackup(Path.Combine(f.Paths.Backups, backup.FileName!)).IsValid);
        f.Sql("UPDATE posters SET content_layout_json=NULL;");
        Assert.False(f.Store.Initialize().Succeeded);
    }

    [Fact] public void UnrelatedPosterEditDoesNotInvalidateContentPreview()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); string b = f.NewPoster(); var command = f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry());
        var p = f.Current.PostersById[b];
        f.Coordinator.Execute(new PosterStateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, PosterStateCommandKind.Rename,
            f.Current.Revision, b, p.Poster with { Title = "unrelated" }) { ReadSet = StateReadSet.ForPoster(b, p.EntityVersion) });
        f.Commit(command);
    }

    [Fact] public void LockedPosterRejectsAdditionWithoutAllocatingReferences()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); var p = f.Current.PostersById[f.Main];
        f.Coordinator.Execute(new PosterStateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, PosterStateCommandKind.SetLocked,
            f.Current.Revision, f.Main, p.Poster with { IsLocked = true }) { ReadSet = StateReadSet.ForPoster(f.Main, p.EntityVersion) });
        Assert.Equal(StateCommandStatus.PermissionDenied, f.Coordinator.Execute(f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry())).Status);
        Assert.Empty(f.Current.ContentCatalog.Items);
    }

    [Fact] public void AdvertisedReferenceCannotBypassVirtualStreamRestriction()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); var entry = ContentReferencePersistenceTests.Entry();
        entry = entry with { Item = entry.Item with { Kind = ContentKind.VirtualStream, Target = new(ContentTargetKind.Virtual, "temporary-stream") } };
        Assert.False(f.Coordinator.Execute(f.AddCommand(f.Main, entry)).Succeeded); Assert.Empty(f.Current.ContentCatalog.Items);
    }

    [Fact] public void OfflineRestoreRecoversSharedIdentityAndLayoutTogether()
    {
        using var f = new ContentReferencePersistenceTests.Fixture(); var entry = ContentReferencePersistenceTests.Entry();
        f.Commit(f.AddCommand(f.Main, entry)); var backup = f.Store.CreateBackup("before-repair"); Assert.True(backup.Succeeded, backup.Error);
        f.Commit(f.AddCommand(f.Main, ContentReferencePersistenceTests.Entry("different.txt")) with { RepairContentId = entry.Item.Id });
        f.Store.Dispose();
        var recovery = new StateBackupRecovery(f.Paths, () => true).Restore(backup.FileName!); Assert.True(recovery.Succeeded, recovery.Error);
        using var restored = new SqliteStateStore(f.Paths); Assert.True(restored.Initialize().Succeeded);
        var state = new SqliteStateCommandRepository(restored).Read();
        Assert.Equal(entry.Item, Assert.Single(state.ContentCatalog.Items)); Assert.Single(state.ContentCatalog.PosterItems);
        Assert.Equal(1, state.ContentVersion);
    }

    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp.Reference.Probe.Tests", Guid.NewGuid().ToString("N"));
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
