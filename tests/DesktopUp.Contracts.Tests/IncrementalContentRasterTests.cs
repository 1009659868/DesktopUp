using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;
using static DesktopUp.Contracts.Tests.ContentIconPersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class IncrementalContentRasterTests
{
    private sealed class Worker : ILocalContentWorker
    {
        public int Calls;
        public TaskCompletionSource? Hold;
        public async ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken cancellation)
        {
            Calls++;
            if (Hold is { } hold) await hold.Task; // Deliberately cancellation-resistant.
            return new(request.RequestId, ContentAvailability.Available, "", "observed " + request.Item.DisplayName,
                "observed type", [0, 0, 0, 255], 1, 1);
        }
    }
    private static ContentLayoutDocument Layout(Fixture f)
    {
        Assert.True(ContentLayoutPolicy.TryRead(f.Current.PostersById[f.Main].Poster.ContentLayoutJson, f.Main, out var doc, out _));
        return doc!;
    }
    private static ContentAssetKey[] Keys(Fixture f, ReferenceContentRuntime source) => source.Read(Layout(f))
        .Select(v => source.RasterKey(new(f.Main, v.ItemId, 1, Layout(f).DocumentVersion, Layout(f).ItemSetVersion,
            1, v.AssetKey!, 100, 120, 96, 96, 1, source.Version))).ToArray();

    [Fact]
    public async Task LayoutSelectionSizeThemeAndMetadataDoNotRestartIntrinsicRasterWork()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry()));
        var worker = new Worker(); var source = new ReferenceContentRuntime(f.Current.ContentCatalog, f.Main, 1, worker);
        var key = Keys(f, source).Single(); using var cache = new ContentAssetCache(source); cache.SetVisible([key]);
        await cache.RequestAsync(key);
        Assert.True(source.Version > 1); // Name/type observations are published, without a pixel generation change.
        Assert.Equal(key.SourceKey, ContentSourceProjection.Read(source, Layout(f), expectedVersion: 1).Single().AssetKey);
        for (int i = 2; i <= 40; i++)
        {
            var next = source.RasterKey(key with { DocumentVersion = i, ItemSetVersion = i, PlacementVersion = i,
                ThemeVersion = i, SourceVersion = source.Version, PixelWidth = 300, PixelHeight = 400, DpiX = 144 });
            Assert.Equal(key, next); cache.SetVisible([next]);
            Assert.Equal(ContentAssetStatus.Cached, (await cache.RequestAsync(next)).Status);
        }
        Assert.Equal(1, worker.Calls);
        Assert.NotEqual(key, source.RasterKey(key with { SessionEpoch = 2 }));
        Assert.False(source.IsCurrentRaster(key with { PosterId = "another" }));
    }

    [Fact]
    public async Task BatchCompletionsCanPublishMetadataWithoutCancellingOtherFourSlotLoads()
    {
        using var f = new Fixture();
        f.Commit(f.AddCommand(f.Main, Enumerable.Range(0, 20).Select(i => Entry($"batch-{i}.txt")).ToArray()));
        var worker = new Worker { Hold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var source = new ReferenceContentRuntime(f.Current.ContentCatalog, f.Main, 1, worker);
        using var cache = new ContentAssetCache(source);
        source.Changed += (_, _) => cache.SetVisible(Keys(f, source));
        var keys = Keys(f, source); cache.SetVisible(keys);
        var first = keys.Select(cache.RequestAsync).ToArray();
        Assert.Equal(4, worker.Calls); Assert.Equal(4, cache.RunningLoads);
        worker.Hold.SetResult(); await Task.WhenAll(first);
        foreach (var key in keys)
            Assert.Contains((await cache.RequestAsync(key)).Status, new[] { ContentAssetStatus.Loaded, ContentAssetStatus.Cached });
        Assert.Equal(20, worker.Calls); Assert.Equal(0, cache.RunningLoads);
        Assert.All(keys, key => Assert.True(cache.TryGet(key, out var pixels) && pixels is not null));
    }

    [Fact]
    public async Task CatalogAdditionAndRemovalPreserveUnchangedMembersAndCachedPixels()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry()));
        var worker = new Worker(); var source = new ReferenceContentRuntime(f.Current.ContentCatalog, f.Main, 1, worker);
        using var cache = new ContentAssetCache(source); var first = Keys(f, source).Single(); cache.SetVisible([first]);
        await cache.RequestAsync(first);
        var unrelated = f.Current.ContentCatalog with { Items = [.. f.Current.ContentCatalog.Items, Entry("unbound.txt").Item] };
        long version = source.Version;
        Assert.False(source.Reconcile(unrelated)); Assert.Equal(version, source.Version);
        string otherPoster = f.NewPoster();
        f.Commit(f.AddCommand(otherPoster, Entry("other-poster.txt")));
        Assert.False(source.Reconcile(f.Current.ContentCatalog)); Assert.Equal(version, source.Version);
        for (int i = 0; i < 12; i++)
        {
            f.Commit(f.AddCommand(f.Main, Entry($"extra-{i}.txt")));
            Assert.True(source.Reconcile(f.Current.ContentCatalog));
            var keys = Keys(f, source); cache.SetVisible(keys);
            foreach (var key in keys) await cache.RequestAsync(key);
            Assert.Contains(first, keys); Assert.Equal(i + 2, worker.Calls);
        }
        var retained = f.Current.ContentCatalog.PosterItems.Where(p => p.LayoutItemId == first.ItemId).ToArray();
        Assert.True(source.Reconcile(f.Current.ContentCatalog with { PosterItems = retained }));
        Assert.True(source.IsCurrentRaster(first)); cache.SetVisible([first]);
        Assert.Equal(ContentAssetStatus.Cached, (await cache.RequestAsync(first)).Status);
        Assert.Equal(13, worker.Calls);
    }

    [Theory]
    [InlineData("icon")]
    [InlineData("source")]
    [InlineData("refresh")]
    [InlineData("remove")]
    public async Task ChangedMemberRejectsLateRasterAndObservationWithoutLosingOtherMembers(string change)
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        f.Commit(f.AddCommand(f.Main, Entry("unaffected.txt")));
        var worker = new Worker { Hold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var source = new ReferenceContentRuntime(f.Current.ContentCatalog, f.Main, 1, worker);
        var keys = Keys(f, source); var old = keys[0]; var other = keys[1];
        var pending = source.LoadAsync(old, CancellationToken.None).AsTask();
        switch (change)
        {
            case "icon":
                var catalog = f.Current.ContentCatalog;
                source.Reconcile(catalog with { PosterItems = catalog.PosterItems.Select(p => p.LayoutItemId == old.ItemId ? p with { IconAssetId = Icon().Id } : p).ToArray() });
                break;
            case "source":
                f.Commit(f.AddCommand(f.Main, Entry("repaired.txt")) with { RepairContentId = entry.Item.Id });
                source.Reconcile(f.Current.ContentCatalog); break;
            case "refresh": source.Refresh(); break;
            case "remove": source.Reconcile(f.Current.ContentCatalog with { PosterItems = f.Current.ContentCatalog.PosterItems.Where(p => p.LayoutItemId != old.ItemId).ToArray() }); break;
        }
        Assert.False(source.IsCurrentRaster(old));
        if (change != "refresh") Assert.True(source.IsCurrentRaster(other));
        worker.Hold.SetResult(); Assert.Null(await pending);
        Assert.DoesNotContain(source.Read(Layout(f)), v => v.Name.StartsWith("observed "));
    }
}
