using DesktopUp.Windows;
using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal bool ContentHeadingSurfaceForProbe(bool visible)
    {
        if (_window?.Content is not Microsoft.UI.Xaml.FrameworkElement root || _metaPanel is null || Presenter.Surface is null || _surface is null || _contentStructure is null) return false;
        root.UpdateLayout();
        var content = ReadContentSurfaceForSmoke();
        var origin = Presenter.Surface.TransformToVisual(_surface).TransformPoint(new(0, 0));
        return _presentation.IsContentHeadingVisible == visible &&
            _metaPanel.Visibility == (visible ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed) &&
            _contentStructure.RowDefinitions[1].Height.Value == MetaHeightDip &&
            (visible ? MetaHeightDip > 0 : MetaHeightDip == 0 && _contentStructure.RowDefinitions[1].ActualHeight == 0) &&
            Math.Abs(origin.Y - ContentTopDip) < .51 && content.CanvasMatches && content.InputMatches && content.Clipped;
    }
    internal bool RealIconReadyForProbe => Presenter.Bitmaps.Count > 0;
    internal bool CustomIconForProbe(ContentIconAsset asset)
    {
        var bitmap = Presenter.Bitmaps.FirstOrDefault(p => p.Key.SourceKey == "custom:" + asset.Id).Value;
        if (bitmap is not Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap raster ||
            raster.PixelWidth != asset.Width || raster.PixelHeight != asset.Height) return false;
        using var stream = raster.PixelBuffer.AsStream();
        var pixels = new byte[asset.Pixels.Length]; stream.ReadExactly(pixels);
        return pixels.SequenceEqual(asset.Pixels);
    }
    internal string? SelectOneRealContentForProbe(bool modifier = false)
    {
        string id = ReadContentViews().First().ItemId;
        if (!modifier) return SelectContentItem(id);
        SetContentSelection(id, control: true); RefreshContentSurface(); return null;
    }
    internal object RealCardReadbackForProbe() => Presenter.Cards.Select(pair =>
    {
        var cell = (Microsoft.UI.Xaml.Controls.Grid)pair.Value;
        var card = (Microsoft.UI.Xaml.Controls.Border)cell.Children[0];
        var host = (Microsoft.UI.Xaml.Controls.Grid)card.Child;
        var image = host.Children[0] as Microsoft.UI.Xaml.Controls.Image;
        return new { pair.Key, cell.Width, cell.Height, cell.ActualWidth, cell.ActualHeight,
            border = card.BorderThickness.Left, imageWidth = image?.Width, imageHeight = image?.Height,
            actualImageWidth = image?.ActualWidth, actualImageHeight = image?.ActualHeight,
            stretch = image?.Stretch.ToString(), scale = _monitor?.ScaleX };
    }).ToArray();
    internal bool RealCardPresentationForProbe(bool selected)
    {
        string id = ReadContentViews().First().ItemId;
        if (!Presenter.Cards.TryGetValue(id, out var element) || element is not Microsoft.UI.Xaml.Controls.Grid cell ||
            cell.Children[0] is not Microsoft.UI.Xaml.Controls.Border card ||
            card.Child is not Microsoft.UI.Xaml.Controls.Grid host ||
            host.Children[0] is not Microsoft.UI.Xaml.Controls.Image image ||
            image.Source is not Microsoft.UI.Xaml.Media.Imaging.BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) return false;
        var area = ContentCardPresentationPolicy.ImageArea(cell.Width, cell.Height, Math.Max(20, ContentParameters.LabelHeightDip + 7));
        // WinUI ActualWidth/Height report the aspect-fitted image, not the requested box.
        double fit = Math.Min(area.Width / bitmap.PixelWidth, area.Height / bitmap.PixelHeight);
        double tolerance = .5 / (_monitor?.ScaleX ?? 1) + .01;
        return card.Background is null && Math.Abs(card.BorderThickness.Left - (selected ? 2 : 0)) < .01 &&
            Math.Abs(image.Width - area.Width) < .01 && Math.Abs(image.Height - area.Height) < .01 &&
            Math.Abs(image.ActualWidth - bitmap.PixelWidth * fit) <= tolerance &&
            Math.Abs(image.ActualHeight - bitmap.PixelHeight * fit) <= tolerance &&
            image.Stretch == Microsoft.UI.Xaml.Media.Stretch.Uniform;
    }
    internal bool ReferenceNamesForProbe(IReadOnlyList<string> names) =>
        _contentPorts.Views is ReferenceContentRuntime && _contentPorts.Open is IAsyncContentActions &&
        _contentPorts.UnavailableOpenReason is null &&
        ReadContentViews().Select(i => i.Name).Order().SequenceEqual(names.Order());
}

internal sealed partial class PosterSpatialRuntime
{
    internal bool ContentHeadingSurfaceForProbe(string id, bool visible) => _windows.TryGetValue(id, out var window) && window.ContentHeadingSurfaceForProbe(visible);
    internal bool RealIconReadyForProbe(string id) => _windows.TryGetValue(id, out var window) && window.RealIconReadyForProbe;
    internal bool CustomIconForProbe(string id, ContentIconAsset asset) => _windows.TryGetValue(id, out var window) && window.CustomIconForProbe(asset);
    internal string? SelectOneRealContentForProbe(string id, bool modifier = false) => _windows[id].SelectOneRealContentForProbe(modifier);
    internal object RealCardReadbackForProbe(string id) => _windows[id].RealCardReadbackForProbe();
    internal bool RealCardPresentationForProbe(string id, bool selected) => _windows.TryGetValue(id, out var window) && window.RealCardPresentationForProbe(selected);
    internal bool ReferenceNamesForProbe(string id, params string[] names) =>
        _windows.TryGetValue(id, out var window) && window.ReferenceNamesForProbe(names);
}

public partial class App
{
    // Isolated fixture flag only. Creates two owned sentinel files, never consumes user paths.
    private async void RunContentReferenceProbe(string dataRoot)
    {
        using var lease = _ledger!.Acquire("fixture.content-reference", "non-destructive-reference");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        void Stop() { cancellation.Cancel(); lease.Dispose(); } _stopAutoSmoke = Stop;
        bool passed = false; string? error = null; bool restart = false; int checks = 0;
        void Require(bool ok, string reason) { checks++; if (!ok) throw new InvalidOperationException(reason); }
        async Task Until(Func<bool> predicate, Func<object>? details = null)
        {
            for (int i = 0; !predicate(); i++)
            { if (i > 150) throw new TimeoutException("Reference surface did not become ready. " + JsonSerializer.Serialize(details?.Invoke())); await Task.Delay(50, cancellation.Token); }
        }
        try
        {
            await Until(() => _autoReady && _spatialRuntime is { IsSpatialBusy: false } &&
                _stateCoordinator!.Refresh().Posters.Count == 3);
            var state = _stateCoordinator!.Refresh();
            var posters = state.Posters.OrderBy(p => p.Poster.StackOrder).ToArray();
            string a = posters[0].Poster.PosterId, b = posters[1].Poster.PosterId;
            restart = state.ContentCatalog.Bindings.Count > 0;
            string sourceDirectory = Path.Combine(dataRoot, "reference-sentinels");
            string original = Path.Combine(sourceDirectory, "original.txt"), repaired = Path.Combine(sourceDirectory, "repaired.txt");
            if (!restart)
            {
                Require(!Directory.Exists(sourceDirectory), "Sentinel fixture already exists; preserve it.");
                Directory.CreateDirectory(sourceDirectory);
                foreach (string path in new[] { original, repaired })
                { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write); file.Write("untouched source"u8); }
                var input = LocalContentReferenceProbe.Inspect(original); Require(input.Item is not null, input.Message);
                ContentReferenceCommand Add(string id, ContentReferenceEntry entry)
                {
                    var current = _stateCoordinator.Refresh();
                    return new(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, current.Revision,
                        StateReadSet.ForPoster(id, current.PostersById[id].EntityVersion) with { ContentVersion = current.ContentVersion }, id, [entry], true);
                }
                var first = _stateCommands!.Execute(Add(a, new(input.Item!, ContentReferenceChoice.CreateIndependent)));
                Require(first.Succeeded, first.Error ?? "First add failed.");
                await Until(() => _spatialRuntime!.ReferenceNamesForProbe(a, "original.txt"));
                var reuse = _stateCommands.Execute(Add(b, new(input.Item!, ContentReferenceChoice.Reuse, input.Item!.Id)));
                Require(reuse.Succeeded, reuse.Error ?? "Reuse failed.");
                await Until(() => _spatialRuntime!.ReferenceNamesForProbe(b, "original.txt"));
                var replacement = LocalContentReferenceProbe.Inspect(repaired); Require(replacement.Item is not null, replacement.Message);
                var repair = Add(a, new(replacement.Item!, ContentReferenceChoice.CreateIndependent)) with { RepairContentId = input.Item.Id };
                var before = _stateCoordinator.Refresh();
                repair = repair with { ReadSet = repair.ReadSet with { Posters = new Dictionary<string, long>
                    { [a] = before.PostersById[a].EntityVersion, [b] = before.PostersById[b].EntityVersion } } };
                var result = _stateCommands.Execute(repair); Require(result.Succeeded, result.Error ?? "Shared repair failed.");
                Require(result.CurrentRevision == before.Revision + 1, "Shared repair used multiple revisions.");
                Require(_stateCommands.Execute(repair).Status == StateCommandStatus.Replayed, "Repair replay was not idempotent.");
            }
            await Until(() => _spatialRuntime!.ReferenceNamesForProbe(a, "repaired.txt") && _spatialRuntime.ReferenceNamesForProbe(b, "repaired.txt"));
            await Until(() => _spatialRuntime!.RealIconReadyForProbe(a) && _spatialRuntime.RealIconReadyForProbe(b));
            Require(true, "Actual bounded Shell icons reached both surfaces.");
            await Until(() => _spatialRuntime!.RealCardPresentationForProbe(a, false), () => _spatialRuntime!.RealCardReadbackForProbe(a));
            Require(true, "Unselected real icon fills its available image area proportionally.");
            Require(_spatialRuntime!.SelectOneRealContentForProbe(a) is null, "Single selection failed.");
            await Until(() => _spatialRuntime.RealCardPresentationForProbe(a, false), () => _spatialRuntime.RealCardReadbackForProbe(a));
            Require(true, "Plain single selection has no browsing outline or background.");
            Require(_spatialRuntime.SelectAllRealContent(a, clear: true) is null, "Clear before modifier selection failed.");
            Require(_spatialRuntime.SelectOneRealContentForProbe(a, modifier: true) is null, "Modifier selection failed.");
            await Until(() => _spatialRuntime.RealCardPresentationForProbe(a, true), () => _spatialRuntime.RealCardReadbackForProbe(a));
            Require(_spatialRuntime.ReadContentEditor(a)?.SelectedIds.Count == 1 &&
                _spatialRuntime.ReadContentEditor(b)?.SelectedIds.Count == 0, "Single outline selection leaked to another poster.");
            Require(_spatialRuntime.SelectAllRealContent(a, clear: true) is null, "Clear selection failed.");
            await Until(() => _spatialRuntime.RealCardPresentationForProbe(a, false));
            if (!restart)
            {
                var extra = LocalContentReferenceProbe.Inspect(original).Item!;
                var before = _stateCoordinator.Refresh();
                var add = new ContentReferenceCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, before.Revision,
                    StateReadSet.ForPoster(a, before.PostersById[a].EntityVersion) with { ContentVersion = before.ContentVersion },
                    a, [new(extra, ContentReferenceChoice.CreateIndependent)], true);
                Require(_stateCommands!.Execute(add).Succeeded, "Extra add failed.");
                await Until(() => _spatialRuntime!.ReferenceNamesForProbe(a, "repaired.txt", "original.txt"));
                Require(_spatialRuntime!.SelectAllRealContent(a) is null && _spatialRuntime.ReadContentEditor(a)?.SelectedIds.Count == 2,
                    "Select-all did not reach the real view.");
                before = _stateCoordinator.Refresh();
                var member = before.ContentCatalog.Memberships.Single(m => m.ContentItemId == extra.Id);
                string local = before.ContentCatalog.PosterItems.Single(p => p.MembershipId == member.Id).LayoutItemId;
                var detach = new ContentDetachCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, before.Revision,
                    StateReadSet.ForPoster(a, before.PostersById[a].EntityVersion) with { ContentVersion = before.ContentVersion }, a, [local], true);
                Require(_spatialRuntime.DetachContent(detach) is null, "Surface detach failed.");
                await Until(() => _spatialRuntime.ReferenceNamesForProbe(a, "repaired.txt"));
                Require(_stateCoordinator.Refresh().Revision == before.Revision + 1, "Detach was not a single revision.");
            }
            state = _stateCoordinator.Refresh();
            Require(state.ContentCatalog.Items.Count == 2 && state.ContentCatalog.PosterItems.Count == 2 && state.ContentCatalog.Bindings.Count == 2,
                "Persistent relation cardinality differs.");
            var orphanPreview = ContentReferencePreviewPolicy.ForItem(state.ContentCatalog, LocalContentReferenceProbe.Inspect(original).Item!, []);
            Require(orphanPreview.Reusable.Count == 0 && orphanPreview.DefaultChoice == ContentReferenceChoice.CreateIndependent,
                "Detached-only history was incorrectly offered as reuse.");
            var sharedPreview = ContentReferencePreviewPolicy.ForItem(state.ContentCatalog, LocalContentReferenceProbe.Inspect(repaired).Item!, []);
            Require(sharedPreview.Reusable.Count == 1 && sharedPreview.Reusable[0].PosterIds.Order().SequenceEqual(new[] { a, b }.Order()),
                "Active shared reference owners are missing from the preview.");
            Require(File.ReadAllText(original) == "untouched source" && File.ReadAllText(repaired) == "untouched source", "Source was modified.");
            Require(state.PostersById[posters[2].Poster.PosterId].Poster.ContentLayoutJson == posters[2].Poster.ContentLayoutJson,
                "Unrelated poster layout changed.");
            _controlCenter!.Show(); await Task.Delay(200, cancellation.Token);
            Require(_controlCenter.WindowForPortableProbe is not null, "Control center failed to open.");
            var center = _controlCenter.WindowForPortableProbe!;
            center.SelectSettingsTargetForProbe(a);
            await Until(() => center.ContentHeadingControlsForProbe(!restart) && _spatialRuntime!.ContentHeadingSurfaceForProbe(a, !restart));
            Require(true, "Heading initial/restarted state matches actual XAML and published input.");
            var retained = _stateCoordinator.Refresh();
            foreach (bool visible in restart ? new[] { true, false } : new[] { false, true, false })
            {
                long revision = _stateCoordinator.Refresh().Revision;
                center.SetContentHeadingForProbe(visible);
                await Until(() => center.ContentHeadingControlsForProbe(visible) && _spatialRuntime!.ContentHeadingSurfaceForProbe(a, visible));
                state = _stateCoordinator.Refresh();
                Require(state.Revision == revision + 1, "Heading toggle was not a single committed revision.");
                Require(state.PostersById[b].Poster == retained.PostersById[b].Poster &&
                    state.PostersById[a].Poster.ContentLayoutJson == retained.PostersById[a].Poster.ContentLayoutJson &&
                    state.ContentVersion == retained.ContentVersion, "Heading edit changed another poster, authoring or real references.");
            }
            // V1 exercises the existing production store/provider/bitmap path, not an upload UI.
            var icon = ContentIconAsset.Create(64, 32, Enumerable.Range(0, 64 * 32)
                .SelectMany(_ => new byte[] { 120, 30, 10, 128 }).ToArray());
            state = _stateCoordinator.Refresh();
            Require(state.ContentCatalog.PosterItems.Single(p => p.PosterId == a).IconAssetId == (restart ? icon.Id : null),
                "Custom icon did not survive the cold restart.");
            if (restart) await Until(() => _spatialRuntime!.CustomIconForProbe(a, icon));
            var iconBefore = state;
            void SetIcon(ContentIconAsset? desired)
            {
                var live = _stateCoordinator.Refresh();
                var card = live.ContentCatalog.PosterItems.Single(p => p.PosterId == a);
                var witness = ContentRelationPolicy.Resolve(live.ContentCatalog, a, card.LayoutItemId)!.Witness;
                var edit = new ContentIconCommand(Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter, live.Revision,
                    StateReadSet.ForPoster(a, live.PostersById[a].EntityVersion) with { ContentVersion = live.ContentVersion },
                    witness, desired, true);
                var result = ((StateProjectionCoordinator)_stateCoordinator).Execute(edit, cancellation.Token);
                Require(result.Succeeded, result.Error ?? "Icon command failed.");
                Require(result.CurrentRevision == live.Revision + 1, "Icon edit was not one revision.");
            }
            if (restart) SetIcon(null);
            SetIcon(icon);
            await Until(() => _spatialRuntime!.CustomIconForProbe(a, icon));
            Require(true, "Owned BGRA bytes reached the actual WriteableBitmap.");
            SetIcon(null);
            await Until(() => _spatialRuntime!.RealIconReadyForProbe(a) && !_spatialRuntime.CustomIconForProbe(a, icon));
            Require(true, "Restore default replaced custom bitmap with provider icon.");
            SetIcon(icon);
            await Until(() => _spatialRuntime!.CustomIconForProbe(a, icon));
            state = _stateCoordinator.Refresh();
            Require(state.ContentCatalog.Items.SequenceEqual(iconBefore.ContentCatalog.Items) &&
                state.PostersById[a].Poster == iconBefore.PostersById[a].Poster &&
                state.PostersById[b].Poster == iconBefore.PostersById[b].Poster &&
                state.PostersById[b].EntityVersion == iconBefore.PostersById[b].EntityVersion &&
                state.ContentCatalog.PosterItems.Single(p => p.PosterId == b).IconAssetId is null,
                "Icon editing changed a target, layout, template or another poster.");
            passed = true;
        }
        catch (Exception failure) { error = failure.ToString(); }
        finally { if (_stopAutoSmoke == Stop) _stopAutoSmoke = null; }
        var report = new { passed, restart, checks, error, nativeInput = false, shellExecuted = false, userFiles = false };
        File.WriteAllText(Path.Combine(dataRoot, restart ? "references-restart.json" : "references-add.json"), JsonSerializer.Serialize(report));
        lease.Dispose(); _log!.Write(passed ? "info" : "error", "content.references_observed", report); ExitApplication();
    }
}
