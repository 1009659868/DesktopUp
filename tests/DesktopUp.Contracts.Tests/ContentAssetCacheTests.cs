using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentAssetCacheTests
{
    [Fact]
    public async Task Null_provider_miss_is_cached_and_nonvisible_keys_never_start_work()
    {
        using var cache = new ContentAssetCache(NullContentAssetProvider.Instance);
        var key = Key();
        Assert.Equal(ContentAssetStatus.Stale, (await cache.RequestAsync(key)).Status);
        cache.SetVisible(new[] { key });
        Assert.Equal(ContentAssetStatus.Missing, (await cache.RequestAsync(key)).Status);
        Assert.True(cache.TryGet(key, out var payload)); Assert.Null(payload);
        Assert.Equal(1, cache.Count); Assert.Equal(0, cache.CachedBytes); Assert.Equal(0, cache.RunningLoads);
        Assert.Equal(ContentAssetStatus.Missing, (await cache.RequestAsync(key)).Status);
    }

    [Fact]
    public async Task Identical_inflight_requests_coalesce_and_payload_is_copied_before_cache_ownership()
    {
        var provider = new ControlledProvider(); using var cache = new ContentAssetCache(provider);
        var key = Key(); cache.SetVisible(new[] { key });
        Task<ContentAssetResult> a = cache.RequestAsync(key), b = cache.RequestAsync(key);
        Assert.Same(a, b); Assert.Equal(1, provider.Calls);
        byte[] bytes = { 1, 2, 3 }; provider.Complete(new("image/png", bytes));
        Assert.Equal(ContentAssetStatus.Loaded, (await a).Status);
        bytes[0] = 99;
        Assert.True(cache.TryGet(key, out var stored)); Assert.Equal(1, stored!.Bytes.Span[0]);
        Assert.Equal(ContentAssetStatus.Cached, (await cache.RequestAsync(key)).Status);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("owner")] [InlineData("item")] [InlineData("session")] [InlineData("document")]
    [InlineData("membership")] [InlineData("placement")] [InlineData("source")]
    [InlineData("width")] [InlineData("height")] [InlineData("dpi")] [InlineData("theme")] [InlineData("source-version")]
    public async Task Late_result_cannot_update_different_identity_version_or_render_key(string changed)
    {
        var provider = new ControlledProvider(); using var cache = new ContentAssetCache(provider);
        var old = Key(); var next = changed switch
        {
            "owner" => old with { PosterId = "B", ItemId = "B:item-0" },
            "item" => old with { ItemId = "A:item-1" },
            "session" => old with { SessionEpoch = 2 },
            "document" => old with { DocumentVersion = 2 },
            "membership" => old with { ItemSetVersion = 2 },
            "placement" => old with { PlacementVersion = 2 },
            "source" => old with { SourceKey = "demo-other" },
            "width" => old with { PixelWidth = 128 },
            "height" => old with { PixelHeight = 128 },
            "dpi" => old with { DpiX = 144, DpiY = 144 },
            "source-version" => old with { SourceVersion = 2 },
            _ => old with { ThemeVersion = 2 }
        };
        cache.SetVisible(new[] { old }); var pending = cache.RequestAsync(old);
        cache.SetVisible(new[] { next });
        Assert.True(provider.Token.IsCancellationRequested);
        Assert.Equal(ContentAssetStatus.Stale, (await pending).Status);
        provider.Complete(new("image/png", new byte[] { 1 })); await Drain(cache);
        Assert.False(cache.TryGet(old, out _)); Assert.False(cache.TryGet(next, out _));
        Assert.Equal(0, cache.Count); Assert.Equal(0, cache.CachedBytes);
    }

    [Fact]
    public async Task Cancellation_resistant_provider_still_occupies_real_concurrency_slot()
    {
        var provider = new ControlledProvider(); using var cache = new ContentAssetCache(provider, new(MaximumConcurrentLoads: 1));
        var old = Key(); var next = old with { DocumentVersion = 2 };
        cache.SetVisible(new[] { old }); _ = cache.RequestAsync(old);
        cache.SetVisible(new[] { next });
        Assert.Equal(1, cache.RunningLoads);
        Assert.Equal(ContentAssetStatus.Capacity, (await cache.RequestAsync(next)).Status);
        Assert.Equal(1, provider.Calls);
        provider.Complete(null); await Drain(cache);
        Assert.Equal(0, cache.RunningLoads);
    }

    [Fact]
    public async Task Dispose_cancels_pending_work_clears_cache_and_discards_ignored_cancellation_completion()
    {
        var provider = new ControlledProvider(); var cache = new ContentAssetCache(provider); var key = Key();
        cache.SetVisible(new[] { key }); var pending = cache.RequestAsync(key);
        cache.Dispose(); cache.Dispose();
        Assert.True(provider.Token.IsCancellationRequested);
        Assert.Equal(ContentAssetStatus.Stale, (await pending).Status);
        provider.Complete(new("image/png", new byte[] { 1, 2 })); await Drain(cache);
        Assert.Equal(0, cache.Count); Assert.Equal(0, cache.CachedBytes); Assert.Equal(0, cache.RunningLoads);
        Assert.Equal(ContentAssetStatus.Stale, (await cache.RequestAsync(key)).Status);
    }

    [Theory] [InlineData("image/svg+xml", 2)] [InlineData("image/png", 5)] [InlineData("image/png", 0)]
    public async Task Unsupported_or_oversized_payload_is_not_retained(string mime, int bytes)
    {
        using var cache = new ContentAssetCache(new ImmediateProvider(new(mime, new byte[bytes])),
            new(MaximumBytes: 8, MaximumPayloadBytes: 4));
        var key = Key(); cache.SetVisible(new[] { key });
        Assert.Equal(ContentAssetStatus.Failed, (await cache.RequestAsync(key)).Status);
        Assert.Equal(0, cache.Count); Assert.Equal(0, cache.CachedBytes); Assert.Equal(0, cache.RunningLoads);
    }

    [Fact]
    public async Task Lru_entry_and_byte_limits_are_enforced_independently()
    {
        var keys = Enumerable.Range(0, 4).Select(i => Key() with { ItemId = "A:item-" + i }).ToArray();
        using var cache = new ContentAssetCache(new ImmediateProvider(new("image/png", new byte[3])),
            new(MaximumEntries: 3, MaximumBytes: 7, MaximumPayloadBytes: 3));
        cache.SetVisible(keys);
        await cache.RequestAsync(keys[0]); await cache.RequestAsync(keys[1]);
        Assert.True(cache.TryGet(keys[0], out _)); // recently used: evict 1 next
        await cache.RequestAsync(keys[2]);
        Assert.True(cache.TryGet(keys[0], out _)); Assert.False(cache.TryGet(keys[1], out _)); Assert.True(cache.TryGet(keys[2], out _));
        Assert.Equal(2, cache.Count); Assert.Equal(6, cache.CachedBytes);
        using var misses = new ContentAssetCache(NullContentAssetProvider.Instance, new(MaximumEntries: 2));
        misses.SetVisible(keys); foreach (var key in keys) await misses.RequestAsync(key);
        Assert.Equal(2, misses.Count); Assert.Equal(0, misses.CachedBytes);
    }

    [Fact]
    public void Invalid_keys_and_visibility_budget_fail_before_provider_work()
    {
        using var cache = new ContentAssetCache(NullContentAssetProvider.Instance, new(MaximumVisibleKeys: 1));
        Assert.Throws<ArgumentException>(() => cache.SetVisible(new[] { Key(), Key() with { ItemId = "A:item-2" } }));
        Assert.Throws<ArgumentException>(() => cache.SetVisible(new[] { Key() with { ItemId = "B:foreign" } }));
        Assert.Throws<ArgumentException>(() => cache.SetVisible(new[] { Key() with { PixelWidth = 4097 } }));
        Assert.Equal(0, cache.RunningLoads);
    }

    [Fact]
    public async Task SlotNotificationWaitsForActualCompletionAndCannotFaultCache()
    {
        var provider = new ControlledProvider(); using var cache = new ContentAssetCache(provider);
        var notification = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.LoadFinished += () =>
        {
            notification.TrySetResult(cache.RunningLoads);
            throw new InvalidOperationException("A failing observer must not fault the provider task.");
        };
        var key = Key(); cache.SetVisible([key]); var pending = cache.RequestAsync(key);
        cache.SetVisible([]);
        Assert.Equal(ContentAssetStatus.Stale, (await pending).Status);
        Assert.False(notification.Task.IsCompleted);
        provider.Complete(null);
        Assert.Equal(0, await notification.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task SameKeyReentryStillReportsCancelledRealFlightUntilItFinishes()
    {
        var provider = new ControlledProvider(); using var cache = new ContentAssetCache(provider);
        var key = Key(); cache.SetVisible([key]); _ = cache.RequestAsync(key);
        cache.SetVisible([]); cache.SetVisible([key]);
        Assert.True(cache.IsLoading(key)); Assert.Equal(1, cache.RunningLoads);
        Assert.Equal(ContentAssetStatus.Capacity, (await cache.RequestAsync(key)).Status);
        provider.Complete(null); await Drain(cache);
        Assert.False(cache.IsLoading(key)); Assert.False(cache.TryGet(key, out _));
        Assert.Equal(ContentAssetStatus.Missing, (await cache.RequestAsync(key)).Status);
        Assert.Equal(2, provider.Calls);
    }

    private static ContentAssetKey Key() => new("A", "A:item-0", 1, 1, 1, 1, "demo-neutral", 64, 64, 96, 96, 1);
    private static async Task Drain(ContentAssetCache cache)
    {
        for (int i = 0; cache.RunningLoads > 0 && i < 1000; i++) await Task.Delay(1);
        Assert.Equal(0, cache.RunningLoads);
    }
    private sealed class ControlledProvider : IContentAssetProvider
    {
        private readonly TaskCompletionSource<ContentAssetPayload?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        public int Calls { get; private set; }
        public ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken token)
        { Calls++; Token = token; return new(_result.Task); }
        public void Complete(ContentAssetPayload? value) => _result.SetResult(value);
    }
    private sealed class ImmediateProvider(ContentAssetPayload? payload) : IContentAssetProvider
    {
        public ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken cancellationToken) => ValueTask.FromResult(payload);
    }
}
