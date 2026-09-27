using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private void OnContentContextRequested(object? sender, PosterContentContextEventArgs args)
    {
        var request = args.Request;
        if (_disposed || _contentPresentationSuspended || _geometry.IsCollapsed || _inputIsland is null ||
            request.Session != inputSession || request.Hit.LayoutRevision != _inputIsland.AppliedLayoutRevision ||
            request.Frame != _bounds || inputRouter.ResolveContext(request.Hit, request.ScreenPoint, [inputSession], request.PressScreenPoint) is null ||
            request.ContentSnapshotGeneration != _contentSnapshot?.ViewportGeneration ||
            !inputRouter.IsCurrent(inputSession, request.Hit.LayoutRevision)) return;
        if (SelectContentItem(request.ContentItemId) is { } error)
        { ReportContentFailure(error); return; }
        args.Handled = true;
        PublishContentFeedback(null);
        var pinAction = ResolveContentPinContext(request);
        if (pinAction == ContentPinContextAction.Cancel) return;
        if (pinAction == ContentPinContextAction.ReleasePin)
        {
            // Same formal command, CAS, lock/ownership checks and undo history as the item
            // menu. Legacy hard-position locks never enter this shortcut.
            if (EditContent(new(ContentLayoutEditKind.Pinned, request.ContentItemId) { Enabled = false }) is { } unpinError)
                ReportContentFailure(unpinError);
            return;
        }
        ShowPosterMenu(request.ContentItemId, request.ScreenPoint);
    }

    private ContentPinContextAction ResolveContentPinContext(PosterContentContextRequest request)
    {
        if (request.PressScreenPoint is not { } press || _monitor is null || _contentSnapshot is null || _contentViewport is null)
            return ContentPinContextAction.ItemMenu;
        var placement = ContentDocument.Placements.FirstOrDefault(p => p.ItemId == request.ContentItemId);
        if (placement is null || ContentPinPresentationPolicy.Adornment(ContentDocument, placement) != ContentPinAdornment.OrderPin)
            return ContentPinContextAction.ItemMenu;
        (double X, double Y) Local(PointerScreenPoint point) =>
            ((point.X-_bounds.X)/_monitor.ScaleX-ContentLeftDip, (point.Y-_bounds.Y)/_monitor.ScaleY-ContentTopDip);
        var pins = ContentLayoutFlow.ProjectTiles(_contentSnapshot, _contentViewport)
            .Where(tile => tile.ItemId == request.ContentItemId).Select(ContentPinPresentationPolicy.VisibleBounds);
        return ContentPinPresentationPolicy.ResolveContext(pins, Local(press), Local(request.ScreenPoint));
    }

    private void AddContentItemMenu(MenuFlyout flyout, string itemId, Func<string, bool> current)
    {
        var doc = ContentDocument;
        var placement = doc.Placements.First(p => p.ItemId == itemId);
        bool writable = ReadContentEditor().CanEdit && !HasContentGesture && !spatial.IsSpatialBusy &&
            !spatial.HasCenterDraft(doc.PosterId);
        void Add(string title, string command, Func<string?> action, bool enabled = true)
        {
            var item = StyleMenuItem(new MenuFlyoutItem { Text = title, IsEnabled = enabled });
            item.Click += (_, _) =>
            {
                if (!current("item-" + command))
                { flyout.Hide(); ReportContentFailure("所选内容已变化，请重新打开菜单。"); return; }
                flyout.Hide();
                if (action() is { } error) ReportContentFailure(error);
            };
            flyout.Items.Add(item);
        }
        Add("打开（演示）", "open", () =>
        {
            // A menu command is one deliberate activation. Its real focus must not leave an
            // Enter-held activation in the independent center keyboard route.
            ReleaseContentActivationKey();
            try { return ActivateSelectedContent(false); }
            finally { ReleaseContentActivationKey(); }
        }, ContentActivationTargetFor(itemId) is not null);
        flyout.Items.Add(new MenuFlyoutSeparator());
        if (doc.Mode != ContentLayoutMode.Manual)
        {
            Add(placement.FixedPosition ? "旧硬位置 · 保留" : placement.PinnedIndex is { } pin
                    ? $"解除固定顺序（第 {pin} 位）" : "固定当前顺序", "pin",
                () => EditContent(new(ContentLayoutEditKind.Pinned, itemId) { Enabled = placement.PinnedIndex is null }),
                writable && !placement.FixedPosition);
            Add("恢复自动尺寸", "size", () => EditContent(new(ContentLayoutEditKind.SizeOverride, itemId) { Enabled = false }),
                writable && placement.ManualSize && placement.AutomaticSizeBasis is not null);
            flyout.Items.Add(new MenuFlyoutSeparator());
        }
        Add("撤销上次调整", "undo", () => UndoContent(false), writable && _contentHistory.CanUndo(doc));
        Add("重做调整", "redo", () => UndoContent(true), writable && _contentHistory.CanRedo(doc));
    }
}
