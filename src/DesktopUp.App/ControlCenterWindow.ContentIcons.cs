using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private Func<ILocalContentWorker?>? _iconWorker;
    private Func<ContentIconCommand, CancellationToken, StateCommandResult>? _applyIcon;
    private CancellationTokenSource? _iconCancellation;
    internal void ConfigureContentIcons(Func<ILocalContentWorker?> worker,
        Func<ContentIconCommand, CancellationToken, StateCommandResult> apply)
    { _iconWorker = worker; _applyIcon = apply; }

    private ContentIconPreview? CaptureIconPreview()
    {
        var selected = _contentRuntime?.Invoke()?.ReadContentEditor(_view?.SelectedPosterId);
        return _readCommitted?.Invoke() is { } state && selected is not null && selected.SelectedItemId == SelectedContentId
            ? ContentIconPreview.Capture(state, _view?.SelectedPosterId, SelectedContentId, selected.SelectedIds) : null;
    }
    private bool IconPreviewIsCurrent(ContentIconPreview preview)
    {
        var selected = _contentRuntime?.Invoke()?.ReadContentEditor(_view?.SelectedPosterId);
        return !_editingClosed && !_portableStopping && _iconCancellation?.IsCancellationRequested != true &&
            !DesktopInteractionConflicts(preview.PosterId) && _readCommitted?.Invoke() is { } state && selected is not null &&
            selected.SelectedItemId == SelectedContentId && preview.IsCurrent(state, _view?.SelectedPosterId, SelectedContentId, selected.SelectedIds);
    }
    private async Task EditCardIcon(bool restore) => await PortableAction(async () =>
    {
        if (_applyIcon is null || CaptureIconPreview() is not { } preview || !IconPreviewIsCurrent(preview)) return;
        if (restore && !preview.HasCustomIcon) return;
        using var cancellation = new CancellationTokenSource();
        _iconCancellation = cancellation;
        try
        {
            ContentIconAsset? asset = null;
            if (!restore)
            {
                if (_iconWorker?.Invoke() is not { } worker) return;
                var picker = new NativeProfilePicker(WinRT.Interop.WindowNative.GetWindowHandle(this), false,
                    _portableLedger!, _portableLog!, purpose: NativePickerPurpose.CardIcon);
                _portablePicker = picker;
                string? path;
                try { path = await picker.Completion; }
                finally { if (ReferenceEquals(_portablePicker, picker)) _portablePicker = null; }
                if (path is null || !IconPreviewIsCurrent(preview)) return;
                var image = await Task.Run(() => LocalContentReferenceProbe.Inspect(path), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (image.Item is null) throw new InvalidDataException(image.Message);
                asset = await ContentIconImport.DecodeAsync(worker, image.Item, cancellation.Token);
                if (!IconPreviewIsCurrent(preview)) return;
            }
            await ConfirmCardIcon(preview, asset, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_editingClosed && !_portableStopping)
                await PortableDialog("未更改图标", "图像无法导入，原图标保持不变。\n支持本地静态 PNG、JPEG、ICO（不超过 8 MiB）。\n" + error.Message);
        }
        finally { if (ReferenceEquals(_iconCancellation, cancellation)) _iconCancellation = null; }
    });

    private async Task ConfirmCardIcon(ContentIconPreview preview, ContentIconAsset? asset, CancellationToken cancellation)
    {
        if (asset is not null && !await ShowIconPreview(preview, asset)) return;
        // Selection, source and card versions must still match the frozen pre-picker target.
        if (!IconPreviewIsCurrent(preview))
        { await PortableDialog("未更改图标", "卡片或来源已变化，请重新选择并预览。原图标保持不变。"); return; }
        var result = _applyIcon!(preview.Confirm(asset), cancellation);
        if (!result.Succeeded)
            await PortableDialog("未更改图标", result.Error ?? "配置已变化，请重新预览。");
        else
        {
            CommonSettingsNotice.Text = asset is null ? "已恢复来源默认图标 · 真实路径不变" : "已保存当前卡片图标 · 真实路径不变";
            _portableLog?.Write("info", "content.icon_edited", new { preview.PosterId, preview.LayoutItemId, restore = asset is null });
        }
    }

    private async Task<bool> ShowIconPreview(ContentIconPreview preview, ContentIconAsset asset)
    {
        var bitmap = new WriteableBitmap(asset.Width, asset.Height);
        using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(asset.Pixels);
        bitmap.Invalidate();
        var body = new StackPanel { Spacing = 12, MinWidth = 360, MaxWidth = 480 };
        body.Children.Add(new Image { Source = bitmap, Width = 144, Height = 144, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center });
        body.Children.Add(new TextBlock { Text = preview.DisplayName, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBox { Header = "真实来源（只读，不会替换）", Text = preview.SourcePath,
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 96 });
        body.Children.Add(new TextBlock { Text = "仅更换当前卡片的图标。确认后图片存入应用数据，不依赖原图片位置。",
            TextWrapping = TextWrapping.Wrap, Opacity = .75 });
        return await ReferenceDialog("预览卡片图标", body, "使用此图标");
    }
}
