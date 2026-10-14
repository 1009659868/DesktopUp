using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private string? _inlineRenameItem;
    private Action? _cancelInlineRename;
    private Action<TextBox, Popup, Action, Action>? _inlineRenameReadyForProbe;

    private async Task RenameContentInline(string itemId)
    {
        // Let the context-menu Closed handler release its input/stacking lease first.
        await Task.Yield();
        var target = spatial.ContentActionRelation(PosterId!, itemId);
        if (target is null || _disposed || _window?.Content is not FrameworkElement root) return;
        string path = target.Item.Target.Locator;
        string identity = await Task.Run(() => LocalTransferFiles.Identity(path));
        if (_disposed || _contentOpenCancellation.IsCancellationRequested) return;
        string initial = Path.GetFileName(path);
        var snapshot = EnsureContentSnapshot(_bounds);
        var slot = snapshot.Items.FirstOrDefault(p => p.ItemId == itemId);
        if (slot is null || _contentViewport is not { } viewport) return;

        // New content can be below the fold. Bring its complete label into view before
        // opening the editor; never change the saved arrangement or rebuild all cards.
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, viewport, _contentBrowseMode);
        var tile = tiles.FirstOrDefault(t => t.ItemId == itemId && t.Bounds.Y >= 0 &&
            t.Bounds.Bottom <= viewport.HeightDip && t.Bounds.X >= 0 && t.Bounds.Right <= viewport.WidthDip);
        if (tile is null)
        {
            _contentScrollX = Math.Clamp(slot.Bounds.X, 0, Math.Max(0, snapshot.ExtentWidthDip - viewport.WidthDip));
            _itemScrollOffsetDip = Math.Clamp(slot.Bounds.Y, 0, Math.Max(0, snapshot.ExtentHeightDip - viewport.HeightDip));
            RefreshContentSurface();
            snapshot = EnsureContentSnapshot(_bounds);
            viewport = _contentViewport!;
            tile = ContentLayoutFlow.ProjectTiles(snapshot, viewport, _contentBrowseMode).FirstOrDefault(t => t.ItemId == itemId);
        }
        if (tile is null) throw new IOException("当前空间无法显示该项的名称，请扩大海报后重试。");
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        double width = Math.Max(100, tile.Bounds.Width);
        var name = new TextBox { Text = initial, MaxLength = 255, Width = width,
            MinHeight = 26, FontSize = ContentParameters.LabelFontSizeDip, Padding = new(3, 2, 3, 2),
            AcceptsReturn = false, IsSpellCheckEnabled = false };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(name, "重命名 " + initial);
        var notice = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = width, FontSize = 11,
            Foreground = Brush(255, 255, 200, 150), Visibility = Visibility.Collapsed };
        var panel = new StackPanel { Width = width }; panel.Children.Add(name); panel.Children.Add(notice);
        var popup = new Popup { XamlRoot = root.XamlRoot, Child = panel, IsLightDismissEnabled = true, ShouldConstrainToRootBounds = false,
            HorizontalOffset = Math.Clamp(ContentLeftDip + tile.Bounds.X - 3, 0, Math.Max(0, root.ActualWidth - width)),
            VerticalOffset = ContentTopDip + tile.Bounds.Bottom - Math.Max(20, ContentParameters.LabelHeightDip + 7) - 2 };
        DesktopMenuInteraction? interaction = null;
        bool saving = false, finished = false, focused = false;
        Task saveOperation = Task.CompletedTask;
        void QueueSave(bool leaving) { if (!saving && !finished) saveOperation = Save(leaving); }
        void Finish()
        {
            if (finished) return;
            finished = true; popup.IsOpen = false; done.TrySetResult();
        }
        void Cancel() => Finish(); // New object remains; only the naming edit is cancelled.
        async Task Save(bool leaving)
        {
            if (finished || saving) return;
            string value = name.Text;
            if (string.IsNullOrWhiteSpace(value) || value == initial) { Finish(); return; }
            if (LocalFileNamePolicy.Error(value) is { } invalid)
            {
                if (leaving) { Finish(); return; }
                notice.Text = invalid; notice.Visibility = Visibility.Visible; return;
            }
            saving = true; name.IsReadOnly = true;
            try
            {
                await spatial.RenameContent(PosterId!, itemId, target.Item.Id, target.Item.Version, value, identity);
                Finish();
            }
            catch (Exception error)
            {
                log.Write("warning", "content.inline_rename_failed", new { PosterId, error.Message });
                if (leaving || _disposed || finished) { bool show = !finished && !_disposed; Finish(); if (show) await ShowFileActionError("未完成重命名：" + error.Message); }
                else { notice.Text = error.Message; notice.Visibility = Visibility.Visible; }
            }
            finally { saving = false; name.IsReadOnly = false; }
        }
        _inlineRenameItem = itemId; _cancelInlineRename = Cancel;
        SetInlineNameVisibility(itemId, false);
        using var lease = ledger.Acquire("content.inline-rename", PosterId!);
        using var cancellation = _contentOpenCancellation.Token.Register(() => root.DispatcherQueue.TryEnqueue(Cancel));
        name.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Escape) { e.Handled = true; if (!saving) Cancel(); }
            else if (e.Key == global::Windows.System.VirtualKey.Enter) { e.Handled = true; QueueSave(false); }
        };
        name.LostFocus += (_, _) => { if (focused && !finished) QueueSave(true); };
        popup.Closed += (_, _) => { if (!finished) QueueSave(true); };
        popup.Opened += (_, _) =>
        {
            if (interaction?.ActivatePopup() != true) { Cancel(); return; }
            focused = name.Focus(FocusState.Programmatic);
            name.Select(0, LocalFileNamePolicy.RenameSelectionLength(initial, target.Item.Kind == ContentKind.Folder));
            log.Write("info", "content.inline_rename_opened", new { PosterId, itemId, focused, popup.HorizontalOffset, popup.VerticalOffset });
            _inlineRenameReadyForProbe?.Invoke(name, popup, () => QueueSave(false), Cancel);
        };
        try
        {
            // Reuse the established WinUI Popup bridge, not a second titled Window or an
            // input rectangle behind the native input island. IME/selection remain TextBox-owned.
            interaction = DesktopMenuInteraction.Begin(_windowHandle, PosterInputIslandManager.MenuSurfaces(inputRouter),
                () =>
                {
                    // WM_NCDESTROY can precede XAML Closed/LostFocus on light-dismiss.
                    // A naturally closed editor commits; an invalidated live surface cancels.
                    if (!popup.IsOpen && !finished) QueueSave(true); else Cancel();
                }, action => { _ = root.DispatcherQueue.TryEnqueue(() => action()); }, ledger, log);
            popup.IsOpen = true;
            await done.Task;
        }
        finally
        {
            finished = true; popup.IsOpen = false; interaction?.Dispose(); popup.Child = null;
            await saveOperation; // Shutdown waits for an already accepted rename/rollback.
            _inlineRenameItem = null; _cancelInlineRename = null;
            SetInlineNameVisibility(itemId, true);
            if (!_disposed) _ = ReassertCurrentClusterBottom();
        }
    }

    private void SetInlineNameVisibility(string itemId, bool visible)
    {
        foreach (var entry in _contentCards)
            if (_contentCardPaint.TryGetValue(entry.Key, out var paint) && paint.View.ItemId == itemId && entry.Value is Grid cell)
                foreach (var text in cell.Children.OfType<TextBlock>().Where(t => Grid.GetRow(t) == 1))
                    text.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
