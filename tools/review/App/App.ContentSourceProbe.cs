using System.Collections.Concurrent;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

// Controlled, in-memory adapters; reachable only through the isolated fixture startup guard.
internal sealed class ProbeContentSource : IObservableContentItemViewSource, IContentOpenHandler
{
    public long Version { get; private set; } = 1;
    public event EventHandler? Changed;
    public string? Missing { get; set; }
    public List<ContentOpenIntent> Opens { get; } = [];
    public IReadOnlyList<ContentItemView> Read(ContentLayoutDocument document) => document.Placements.Reverse()
        .Select(p => new ContentItemView(p.ItemId, "假源 " + p.Order, "S" + p.Order, 0xFF609AAF, "fake:shared")
        { IsAvailable = p.ItemId != Missing }).ToArray();
    public void Advance() { Version++; Changed?.Invoke(this, EventArgs.Empty); }
    public void Open(ContentOpenIntent intent) => Opens.Add(intent);
}

internal sealed class ProbeContentAssets : IContentAssetProvider
{
    internal sealed record Pending(ContentAssetKey Key, CancellationToken Token, TaskCompletionSource<ContentAssetPayload?> Completion);
    private readonly ConcurrentQueue<Pending> _pending = new();
    public ConcurrentQueue<ContentAssetKey> Calls { get; } = new();
    public int PendingCount => _pending.Count;
    public ValueTask<ContentAssetPayload?> LoadAsync(ContentAssetKey key, CancellationToken token)
    {
        var completion = new TaskCompletionSource<ContentAssetPayload?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Calls.Enqueue(key); _pending.Enqueue(new(key, token, completion));
        return new(completion.Task); // Intentionally cancellation-resistant; no filesystem/decoder.
    }
    public void CompleteAll()
    {
        while (_pending.TryDequeue(out var pending))
            pending.Completion.TrySetResult(new("image/png", new byte[] { 1, 2, 3, 4 }));
    }
}

internal sealed partial class PosterVisualWindow
{
    private object SourceProbeRead() => new { PosterId, sourceVersion = _contentSession.SourceVersion,
        revision = _stateRevision, document = ContentDocument.DocumentVersion, members = ContentDocument.ItemSetVersion,
        demand = Rasters.VisibleKeys.Values.ToArray(), active = Rasters.Cache?.RunningLoads ?? 0,
        cached = Rasters.Cache?.Count ?? 0, bytes = Rasters.Cache?.CachedBytes ?? 0, attempted = Rasters.Attempted,
        accepted = Rasters.AcceptedCount, HasContentGesture, suspended = _contentSession.IsSuspended, _disposed,
        canvas = ReadBrowseForProbe() };

    internal async Task<object> RunContentSourceForProbeAsync(PosterVisualWindow[] windows, CancellationToken cancellation,
        string dataRoot, Func<string, PosterVisualWindow?> findWindow)
    {
        var source = new ProbeContentSource(); var assets = new ProbeContentAssets();
        var ports = new ContentSourcePorts(source, assets, source);
        var ids = windows.Select(w => w.PosterId!).ToArray();
        var original = ContentDocument;
        var observations = new List<object>(); var cases = new List<string>(); int checks = 0;
        void Require(bool ok, string detail) { checks++; if (!ok) throw new InvalidOperationException("source: " + detail); }
        async Task Until(Func<bool> predicate)
        {
            for (int i = 0; !predicate(); i++)
            { cancellation.ThrowIfCancellationRequested(); if (i > 400) throw new TimeoutException("Source condition timed out."); await Task.Delay(10, cancellation); }
        }
        async Task Drain()
        {
            for (int i = 0; ; i++)
            {
                assets.CompleteAll(); await Task.Delay(10, cancellation);
                if (windows.Where(w => !w._disposed).All(w => w.Rasters.Cache?.RunningLoads is null or 0) && assets.PendingCount == 0) return;
                if (i > 400) throw new TimeoutException("Resource queue failed to quiesce.");
            }
        }
        void Record(string name)
        {
            cases.Add(name); observations.Add(new { name, windows = windows.Where(w => !w._disposed && !w._contentSession.IsSuspended)
                .Select(w => w.SourceProbeRead()).ToArray(), providerCalls = assets.Calls.Count });
        }
        DirectContentProtocolPress Press()
        {
            var surface = inputRouter.ReadSurface(inputSession)!;
            var region = surface.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem);
            var point = new PointerScreenPoint(surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
                surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2);
            var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
            var press = new DirectContentProtocolPress(capture, point);
            HandleContentInput(PosterContentInputPolicy.Event(capture, PosterInputEventKind.Pressed, point, point, surface.Layout.Revision, ++press.Sequence));
            Require(HasContentGesture, "Initial managed press rejected.");
            return press;
        }
        void LateRelease(DirectContentProtocolPress press)
        {
            HandleContentInput(PosterContentInputPolicy.Event(press.Capture, PosterInputEventKind.GestureCompleted,
                press.Point, new(press.Point.X + 100, press.Point.Y + 100), _inputIsland!.AppliedLayoutRevision, ++press.Sequence));
        }
        StateCommandResult Replace(ContentLayoutDocument intent)
        {
            var before = ContentDocument; var desired = RebaseContentIntent(before, intent);
            var result = stateCommands.Execute(new ContentLayoutCommand("source-fixture-" + Guid.NewGuid().ToString("N"), StateCommandSource.Poster,
                _stateRevision, before.PosterId, StateReadSet.ForPoster(before.PosterId, _posterEntityVersion),
                before.DocumentVersion, before.ItemSetVersion, desired, true));
            Require(result.Succeeded, "Explicit membership fixture failed: " + result.Error); return result;
        }
        try
        {
            Require(windows.Length is 1 or 3 or 6 && windows.All(w => !w._geometry.IsCollapsed && !w._geometry.IsLocked), "Use expanded 1/3/6 resource fixture.");
            foreach (var w in windows) spatial.BindContentSource(w.PosterId!, ports);
            await Until(() => windows.All(w => w.Rasters.Cache?.RunningLoads == 4));
            Require(assets.Calls.Count == windows.Length * 4, "Initial per-surface concurrency exceeded.");
            var raw = windows.ToDictionary(w => w.PosterId!, w => w._authoritativePoster!.ContentLayoutJson);
            int oldCalls = assets.Calls.Count;
            for (int i = 0; i < 20; i++) source.Advance();
            await Until(() => windows.All(w => w._contentSession.SourceVersion == source.Version));
            Require(assets.Calls.Count == oldCalls, "Source version churn bypassed real occupied load slots.");
            await Drain();
            Require(windows.All(w => w.Rasters.Attempted == w.Rasters.VisibleKeys.Count &&
                w.Rasters.Cache!.Count <= 64 && w.Rasters.Cache.CachedBytes <= 16 * 1024 * 1024), "Pump did not finish without extra input or exceeded cache.");
            Require(windows.All(w => w.Rasters.VisibleKeys.Values.All(k => k.SourceVersion == source.Version)), "Old source keys returned.");
            Require(windows.All(w => w._authoritativePoster!.ContentLayoutJson == raw[w.PosterId!]), "Metadata reorder wrote layout.");
            Record("bounded-source-churn-and-slot-replenishment");

            string first = ContentDocument.Placements[0].ItemId;
            Require(SelectContentItem(first) is null, "Select failed.");
            int opened = source.Opens.Count;
            Require(ActivateSelectedContent(false) is null && ActivateSelectedContent(true) is null, "Open intent refused.");
            Require(source.Opens.Count == opened + 1, "Key repeat duplicated open intent."); ReleaseContentActivationKey();
            var press = Press(); long revision = _stateRevision;
            source.Missing = _contentGesture!.Slot.ItemId; source.Advance();
            await Until(() => _contentSession.SourceVersion == source.Version && !HasContentGesture);
            LateRelease(press);
            Require(_stateRevision == revision && _contentMotionTimer is null, "Invalidated source gesture wrote data.");
            Require(SelectContentItem(source.Missing) is null && ActivateSelectedContent(false) is not null && source.Opens.Count == opened + 1,
                "Unavailable content opened.");
            source.Missing = null; source.Advance(); await Until(() => _contentSession.SourceVersion == source.Version); await Drain();
            Require(ActivateSelectedContent(false) is null && source.Opens.Count == opened + 2, "Recovery requires restarting the app.");
            ReleaseContentActivationKey(); Record("availability-recovery-gesture-cancel-and-open-dedup");

            press = Press(); var before = ContentDocument; revision = _stateRevision;
            string removed = _contentGesture!.Slot.ItemId;
            var added = new ContentPlacement(before.PosterId + ":source-new", before.Placements.Max(p => p.Order) + 1);
            var committed = Replace(before with { Placements = before.Placements.Where(p => p.ItemId != removed).Append(added).ToArray() });
            Require(!HasContentGesture && committed.CurrentRevision == revision + 1 && ContentDocument.ItemSetVersion == before.ItemSetVersion + 1,
                "Membership did not cancel old gesture or was not a single commit.");
            LateRelease(press);
            Require(_stateRevision == committed.CurrentRevision && !ContentDocument.Placements.Any(p => p.ItemId == removed), "Late release resurrected a member.");
            Require(windows.Skip(1).All(w => w._authoritativePoster!.ContentLayoutJson == raw[w.PosterId!]), "Shared source mixed poster layout ownership.");
            foreach (var w in windows) { var read = w.ReadBrowseForProbe(); Require(read.CanvasMatches && read.InputMatches && read.Clipped, "Metadata/member update broke XAML or hits."); }
            await Drain(); Record("membership-cas-late-release-and-poster-isolation");
            Replace(original); await Drain();
            await CapturePosterXamlForProbeAsync(Path.Combine(dataRoot, "content-source-ready.png"));

            // Metadata notification during the existing R4 group transaction: no mid-animation
            // tree rebuild, latest source version is accepted at its existing restore safe point.
            foreach (bool collapsed in new[] { true, false })
            {
                var completion = new TaskCompletionSource<StateCommandResult>();
                long beforeRevision = _stateRevision, beforeVersion = _contentSession.SourceVersion;
                spatial.SetCollapsed(new("source-posture-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                    PosterStateCommandKind.SetCollapsed, _stateRevision, PosterId!, _authoritativePoster! with { IsCollapsed = collapsed })
                    { ReadSet = StateReadSet.ForPoster(PosterId!, _posterEntityVersion) }, r => completion.TrySetResult(r));
                await Until(() => _transitionLayout is { NativePrepared: true } || completion.Task.IsCompleted);
                Require(_transitionLayout is { NativePrepared: true }, "No intermediate R4 surface observed.");
                var tree = _contentStructure; var layout = _transitionLayout;
                source.Advance(); await Until(() => _contentSession.SourceRefreshPending);
                Require(_contentSession.SourceVersion == beforeVersion && ReferenceEquals(tree, _contentStructure) &&
                    ReferenceEquals(layout, _transitionLayout) && Rasters.VisibleKeys.Count == 0,
                    "Source changed the prepared R4 tree or retained stale resource admission.");
                var posture = await completion.Task.WaitAsync(cancellation);
                Require(posture.Succeeded && posture.CurrentRevision == beforeRevision + 1 && _transitionLayout is null,
                    "R4 did not finish exactly one transaction.");
                await Until(() => _contentSession.SourceVersion == source.Version && !_contentSession.SourceRefreshPending);
                await Drain();
            }
            Record("source-notification-r4-restore-safe-point");

            // Final lifecycle test: a source notification/result after old HWND disposal must not
            // revive that surface. The catalog-owned adapter reconnects to the new session.
            source.Advance(); await Until(() => _contentSession.SourceVersion == source.Version && Rasters.Cache?.RunningLoads == 4);
            var oldCache = Rasters.Cache!; long acceptedBefore = Rasters.AcceptedCount;
            var hide = stateCommands.Execute(new PosterStateCommand("source-hide-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetVisible, _stateRevision, PosterId!, _authoritativePoster! with { IsVisible = false })
                { ReadSet = StateReadSet.ForPoster(PosterId!, _posterEntityVersion) });
            Require(hide.Succeeded, "Hide failed.");
            await Until(() => _disposed || _contentSession.IsSuspended);
            assets.CompleteAll(); await Until(() => oldCache.RunningLoads == 0);
            source.Advance(); await Task.Delay(30, cancellation);
            Require(Rasters.AcceptedCount == acceptedBefore && (Rasters.Cache?.RunningLoads ?? 0) == 0, "Hidden surface accepted a late resource.");
            var hidden = hide.Projection!.PostersById[ids[0]];
            var show = stateCommands.Execute(new PosterStateCommand("source-show-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetVisible, hide.CurrentRevision, ids[0], hidden.Poster with { IsVisible = true })
                { ReadSet = StateReadSet.ForPoster(ids[0], hidden.EntityVersion) });
            Require(show.Succeeded, "Show failed.");
            await Until(() => findWindow(ids[0]) is { _contentSession.IsSuspended: false } shown &&
                ReferenceEquals(shown._contentPorts, ports) && shown._contentSession.SourceVersion == source.Version);
            assets.CompleteAll(); cases.Add("hidden-late-result-and-session-rebind");
            return new { passed = true, checks, cases, posters = windows.Length, observations,
                nativeInput = false, realFiles = false, sourceSchemaFrozen = false, bitmapDecode = false };
        }
        finally
        {
            foreach (string id in ids)
                if (spatial.TryGetProjection(out var state) && state?.PostersById.ContainsKey(id) == true) spatial.BindContentSource(id, ContentSourcePorts.Demo);
            assets.CompleteAll();
        }
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal Task<object> RunContentSourceForProbeAsync(string dataRoot, CancellationToken cancellation)
    {
        var windows = _windows.Values.OrderBy(w => w.PosterId, StringComparer.Ordinal).ToArray();
        return windows[0].RunContentSourceForProbeAsync(windows, cancellation, dataRoot, id => _windows.GetValueOrDefault(id));
    }
}

public partial class App
{
    private async void RunContentSourceProbe(string dataRoot)
    {
        using var lease = _ledger!.Acquire("fixture.content-source", "isolated-source-adapters");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        void Stop() { cancellation.Cancel(); lease.Dispose(); } _stopAutoSmoke = Stop;
        object? observation = null; bool passed = false; string? error = null;
        try
        {
            for (int i = 0; ; i++)
            {
                var current = _stateCoordinator!.Refresh(); cancellation.Token.ThrowIfCancellationRequested();
                if (_autoReady && _spatialRuntime is { IsSpatialBusy: false } && current.Posters.Count is 1 or 3 or 6 &&
                    current.Posters.All(p => _spatialRuntime.ContentVisualReadyForProbe(p.Poster.PosterId))) break;
                if (i > 100) throw new TimeoutException("Resource fixture did not become ready.");
                await Task.Delay(100, cancellation.Token);
            }
            observation = await _spatialRuntime!.RunContentSourceForProbeAsync(dataRoot, cancellation.Token); passed = true;
        }
        catch (Exception failure) { error = failure.ToString(); }
        finally { if (_stopAutoSmoke == Stop) _stopAutoSmoke = null; }
        var report = new { passed, error, observation, nativeInput = false, realFiles = false, uiCProductAccepted = false };
        File.WriteAllText(Path.Combine(dataRoot, "content-source-observation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        lease.Dispose(); _log!.Write(passed ? "info" : "error", "content.source_observed", report); ExitApplication();
    }
}
