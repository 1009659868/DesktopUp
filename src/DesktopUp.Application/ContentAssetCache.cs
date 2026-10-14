namespace DesktopUp.Application;

// SourceKey is an opaque provider key, never a file path or executable instruction.
public sealed record ContentAssetKey(string PosterId, string ItemId, long SessionEpoch, long DocumentVersion,
    long ItemSetVersion, long PlacementVersion, string SourceKey, int PixelWidth, int PixelHeight,
    uint DpiX, uint DpiY, long ThemeVersion, long SourceVersion = 1);
public sealed record ContentAssetPayload(string MediaType, ReadOnlyMemory<byte> Bytes)
{
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
}
public enum ContentAssetStatus { Cached, Loaded, Missing, Stale, Capacity, Failed }
public sealed record ContentAssetResult(ContentAssetKey Key, ContentAssetStatus Status, ContentAssetPayload? Payload = null);
public sealed record ContentAssetCacheOptions(int MaximumEntries = 64, long MaximumBytes = 16 * 1024 * 1024,
    int MaximumPayloadBytes = 4 * 1024 * 1024, int MaximumConcurrentLoads = 4, int MaximumVisibleKeys = 512);

public interface IContentAssetProvider
{
    ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken cancellationToken);
}

// Opt-in for normalized rasters whose pixels do not depend on layout, theme or requested size.
// The provider owns source/member generations; the host still owns visibility and session admission.
public interface IContentRasterIdentity
{
    ContentAssetKey RasterKey(ContentAssetKey demand);
    bool IsCurrentRaster(ContentAssetKey key);
}

// The L-stage production provider deliberately has no filesystem, Shell, decoder or network effects.
public sealed class NullContentAssetProvider : IContentAssetProvider
{
    public static NullContentAssetProvider Instance { get; } = new();
    private NullContentAssetProvider() { }
    public ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken cancellationToken) => ValueTask.FromResult<ContentAssetPayload?>(null);
}

/// <summary>
/// A per-surface bounded cache. SetVisible is the authority for result acceptance, not completion order.
/// Cancellation-resistant providers continue to consume their concurrency slot until they really finish.
/// </summary>
public sealed class ContentAssetCache : IDisposable
{
    private sealed record Entry(ContentAssetPayload? Payload, LinkedListNode<ContentAssetKey> Node);
    private sealed class Flight(ContentAssetKey key)
    {
        public ContentAssetKey Key { get; } = key;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource<ContentAssetResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Valid { get; set; } = true;
    }
    private readonly object _gate = new();
    private readonly IContentAssetProvider _provider;
    private readonly ContentAssetCacheOptions _options;
    private readonly Dictionary<ContentAssetKey, Entry> _entries = new();
    private readonly Dictionary<ContentAssetKey, Flight> _flights = new();
    private readonly LinkedList<ContentAssetKey> _lru = new();
    private HashSet<ContentAssetKey> _visible = new();
    private long _bytes;
    private bool _disposed;

    public ContentAssetCache(IContentAssetProvider provider, ContentAssetCacheOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(provider); _provider = provider; _options = options ?? new();
        if (_options.MaximumEntries is < 1 or > 4096 || _options.MaximumBytes is < 1 or > 256 * 1024 * 1024 ||
            _options.MaximumPayloadBytes < 1 || _options.MaximumPayloadBytes > _options.MaximumBytes ||
            _options.MaximumConcurrentLoads is < 1 or > 16 || _options.MaximumVisibleKeys is < 1 or > 4096)
            throw new ArgumentException("Invalid content asset cache budget.");
    }
    public int Count { get { lock (_gate) return _entries.Count; } }
    public long CachedBytes { get { lock (_gate) return _bytes; } }
    public int RunningLoads { get { lock (_gate) return _flights.Count; } }
    // Includes invalidated-but-not-yet-finished requests. Returning to the same visible key
    // must wait for that real flight rather than enqueue repeated Capacity retries.
    public bool IsLoading(ContentAssetKey key) { lock (_gate) return _flights.ContainsKey(key); }
    // Raised only when a real provider slot is freed, including cancellation-resistant loads.
    // Subscribers marshal to their own dispatcher and recheck the live surface/lifetime.
    public event Action? LoadFinished;

    public void SetVisible(IEnumerable<ContentAssetKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var next = new HashSet<ContentAssetKey>();
        foreach (var key in keys)
        {
            ValidateKey(key);
            if (!next.Add(key)) continue;
            if (next.Count > _options.MaximumVisibleKeys) throw new ArgumentException("Visible asset budget exceeded.");
        }
        List<Flight> cancelled;
        lock (_gate)
        {
            if (_disposed) return;
            _visible = next;
            cancelled = _flights.Values.Where(f => f.Valid && !next.Contains(f.Key)).ToList();
            foreach (var f in cancelled) { f.Valid = false; f.Completion.TrySetResult(new(f.Key, ContentAssetStatus.Stale)); }
        }
        foreach (var f in cancelled) Cancel(f);
    }

    // true + null is a cached miss. This prevents repeated empty-provider work every render frame.
    public bool TryGet(ContentAssetKey key, out ContentAssetPayload? payload)
    {
        lock (_gate)
        {
            payload = null;
            if (_disposed || !_visible.Contains(key) || !_entries.TryGetValue(key, out var entry)) return false;
            Touch(entry); payload = entry.Payload; return true;
        }
    }

    public Task<ContentAssetResult> RequestAsync(ContentAssetKey key)
    {
        ValidateKey(key);
        Flight flight;
        lock (_gate)
        {
            if (_disposed || !_visible.Contains(key)) return Task.FromResult(new ContentAssetResult(key, ContentAssetStatus.Stale));
            if (_entries.TryGetValue(key, out var cached))
            { Touch(cached); return Task.FromResult(new ContentAssetResult(key, cached.Payload is null ? ContentAssetStatus.Missing : ContentAssetStatus.Cached, cached.Payload)); }
            if (_flights.TryGetValue(key, out var running))
                return running.Valid ? running.Completion.Task : Task.FromResult(new ContentAssetResult(key, ContentAssetStatus.Capacity));
            if (_flights.Count >= _options.MaximumConcurrentLoads) return Task.FromResult(new ContentAssetResult(key, ContentAssetStatus.Capacity));
            flight = new(key); _flights.Add(key, flight);
        }
        _ = LoadAsync(flight);
        return flight.Completion.Task;
    }

    private async Task LoadAsync(Flight flight)
    {
        ContentAssetPayload? payload = null;
        ContentAssetStatus status;
        try
        {
            var supplied = await _provider.LoadAsync(flight.Key, flight.Cancellation.Token).ConfigureAwait(false);
            if (supplied is not null)
            {
                if (supplied.MediaType is not ("image/png" or "image/jpeg" or "image/webp" or "image/bgra32") || supplied.Bytes.IsEmpty ||
                    supplied.MediaType == "image/bgra32" && !LocalContentProtocol.ValidPixels(supplied.Bytes.ToArray(), supplied.PixelWidth, supplied.PixelHeight) ||
                    supplied.Bytes.Length > _options.MaximumPayloadBytes)
                    throw new ArgumentException("Unsupported or oversized raster asset payload.");
                payload = supplied with { Bytes = supplied.Bytes.ToArray() };
            }
            status = payload is null ? ContentAssetStatus.Missing : ContentAssetStatus.Loaded;
        }
        catch (OperationCanceledException) { status = ContentAssetStatus.Stale; }
        catch (Exception) { status = ContentAssetStatus.Failed; }
        lock (_gate)
        {
            bool owns = _flights.TryGetValue(flight.Key, out var current) && ReferenceEquals(current, flight);
            if (owns) _flights.Remove(flight.Key);
            if (!owns || _disposed || !flight.Valid || !_visible.Contains(flight.Key)) { payload = null; status = ContentAssetStatus.Stale; }
            else if (status is ContentAssetStatus.Loaded or ContentAssetStatus.Missing)
            {
                long size = payload?.Bytes.Length ?? 0;
                while (_entries.Count >= _options.MaximumEntries || _bytes + size > _options.MaximumBytes) EvictOldest();
                var node = _lru.AddLast(flight.Key); _entries.Add(flight.Key, new(payload, node)); _bytes += size;
            }
            flight.Completion.TrySetResult(new(flight.Key, status, payload));
        }
        flight.Cancellation.Dispose();
        try { LoadFinished?.Invoke(); } catch (Exception) { /* Observer cannot fault a provider task. */ }
    }
    private void Touch(Entry entry) { _lru.Remove(entry.Node); _lru.AddLast(entry.Node); }
    private void EvictOldest()
    {
        if (_lru.First is not { } node) throw new InvalidOperationException("Asset cache budget cannot be satisfied.");
        _bytes -= _entries[node.Value].Payload?.Bytes.Length ?? 0; _entries.Remove(node.Value); _lru.RemoveFirst();
    }
    private static void Cancel(Flight flight)
    {
        try { flight.Cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { } // Provider cancellation callbacks do not own the host lifetime.
    }
    private static void ValidateKey(ContentAssetKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (string.IsNullOrWhiteSpace(key.PosterId) || key.PosterId.Length > 128 || string.IsNullOrWhiteSpace(key.ItemId) ||
            key.ItemId.Length > 192 || !key.ItemId.StartsWith(key.PosterId + ":", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(key.SourceKey) || key.SourceKey.Length > 256 || key.SourceKey.Any(char.IsControl) ||
            key.SessionEpoch < 1 || key.DocumentVersion < 1 || key.ItemSetVersion < 1 || key.PlacementVersion < 1 || key.ThemeVersion < 1 || key.SourceVersion < 1 ||
            key.PixelWidth is < 1 or > 4096 || key.PixelHeight is < 1 or > 4096 || key.DpiX is < 48 or > 960 || key.DpiY is < 48 or > 960)
            throw new ArgumentException("Invalid bounded content asset key.");
    }
    public void Dispose()
    {
        List<Flight> cancelled;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _visible.Clear(); _entries.Clear(); _lru.Clear(); _bytes = 0; LoadFinished = null;
            cancelled = _flights.Values.ToList();
            foreach (var f in cancelled) { f.Valid = false; f.Completion.TrySetResult(new(f.Key, ContentAssetStatus.Stale)); }
        }
        foreach (var f in cancelled) Cancel(f);
    }
}
