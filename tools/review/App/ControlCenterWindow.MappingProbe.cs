using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    // Only called by the owned incoming fixture after its root/marker checks.
    internal async Task VerifyMappingCreationForProbe(string root)
    {
        var before = _readCommitted!.Invoke()!;
        await CreateMappedPosterFromCenter(() => Task.FromResult<string?>(null));
        if (_portableBusy || _readCommitted.Invoke()!.Posters.Count != before.Posters.Count)
            throw new IOException("Cancelled mapping left a lease or created a poster.");
        string directory = Path.Combine(root, "center-map-probe"); Directory.CreateDirectory(directory);
        bool ownedLease = false;
        await CreateMappedPosterFromCenter(() =>
        {
            ownedLease = _portableBusy && _contentRuntime!.Invoke()!.FileServiceReady && !_contentRuntime.Invoke()!.TransfersAvailable;
            return Task.FromResult<string?>(directory);
        });
        var after = _readCommitted.Invoke()!;
        if (!ownedLease || _portableBusy || !CreatePosterButton.IsEnabled || after.Posters.Count != before.Posters.Count + 1 ||
            before.Posters.Any(p => after.PostersById[p.Poster.PosterId].Poster != p.Poster) ||
            _view?.SelectedPosterId == before.Posters.Single().Poster.PosterId ||
            Directory.EnumerateFileSystemEntries(directory).Any())
            throw new IOException("Center mapping did not create/select an independent, untouched directory poster.");
    }
}

internal sealed partial class PosterVisualWindow
{
    internal void VerifyFileActionMenusForProbe()
    {
        string id = ContentDocument.Placements.First().ItemId;
        SelectContentItem(id, preserveSet: false);
        var items = new MenuFlyout(); AddContentItemMenu(items, id, _ => true);
        var names = items.Items.OfType<MenuFlyoutItem>().Select(i => i.Text).ToList();
        if (names.IndexOf("删除") != names.IndexOf("重命名") + 1 || names.IndexOf("属性") != names.IndexOf("删除") + 1)
            throw new IOException("Item delete menu order is incorrect.");
        if (names.Count(n => n == "Win菜单") != 1 || names.Any(n => n.Contains("Windows") && n.Contains("菜单")))
            throw new IOException("Item native menu must use the shared Win菜单 label.");
        var background = new MenuFlyout(); AddContentBackgroundMenu(background, _ => true);
        if (background.Items.OfType<MenuFlyoutItem>().Any(i => i.Text == "目录映射…") ||
            !background.Items.OfType<MenuFlyoutItem>().Any(i => i.Text == "目录属性") ||
            background.Items.OfType<MenuFlyoutItem>().Count(i => i.Text == "Win菜单") != 1)
            throw new IOException("Background menu retained mapping creation or lost directory properties.");
    }
}
