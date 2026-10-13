using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DesktopUp;

// Called only by the isolated-profile guarded UI probes in App.
public sealed partial class ControlCenterWindow
{
    internal bool SettingsControlsEnabledForProbe => AutoPostureSwitch.IsEnabled && AutoPostureDelay.IsEnabled;
    internal bool AppearanceControlsEnabledForProbe => _template.IsEnabled && ScaleSlider.IsEnabled && SpacingSlider.IsEnabled;
    internal void TemplateForProbe(string key) => SelectTemplateKey(key);
    internal void SetTitleVisibilityForProbe(bool show)
    {
        if (!TitleVisibilitySwitch.IsEnabled) throw new InvalidOperationException("Title visibility control is disabled.");
        ShowWorkspacePage(false); LayoutFolder.IsExpanded = true;
        TitleVisibilitySwitch.IsOn = show; // Real ToggleSwitch -> ordinary settings handler.
    }
    internal bool VerifyTitleVisibilityControlForProbe(bool show) =>
        TitleVisibilitySwitch.IsOn == show && TitleVisibilitySwitch.IsEnabled &&
        PosterTitleText.Text == _view?.SelectedPoster?.Poster.Title && !string.IsNullOrWhiteSpace(PosterTitleText.Text);
    internal void SpacingForProbe(double value) { _settingsHeld = true; SpacingSlider.Value = value; }
    internal object PresetDiagnosticsForProbe() => new { scaleMinimum = ScaleSlider.Minimum, scaleMaximum = ScaleSlider.Maximum,
        scaleValue = ScaleSlider.Value, spacingMinimum = SpacingSlider.Minimum, spacingValue = SpacingSlider.Value };
    internal ControlCenterSettingsSession CommonSettingsForProbe => _settings;
    internal void SelectSettingsTargetForProbe(string id) => PosterSelector.SelectedItem =
        ((IReadOnlyList<ControlCenterPosterRow>)PosterSelector.ItemsSource).Single(p => p.PosterId == id);
    internal void PreviewScaleForProbe(double value) { _settingsHeld = true; ScaleSlider.Value = value; }
    internal void FinishSettingsForProbe() { _settingsHeld = false; FinishSettings(); }
    internal void DelayForProbe(double value) => AutoPostureDelay.Value = value;
    internal void ToggleForProbe(bool value) => AutoPostureSwitch.IsOn = value;
    internal void RevealSettingsForProbe() { ShowWorkspacePage(false); ExpandLayoutFolderForProbe(true); }
    internal void RevealBehaviorForProbe() => ShowWorkspacePage(true);
    internal void PresetForProbe(bool spacing, int index)
    {
        var marks = spacing ? _spacingMarks! : _scaleMarks!;
        var slider = spacing ? SpacingSlider : ScaleSlider;
        var point = marks.CenterForProbe(index);
        _settingsHeld = true;
        marks.Begin(1, point);
        slider.Value = marks.Complete(1, point) ?? throw new InvalidOperationException("Preset click missed its native track mark");
        FinishSettingsForProbe();
    }
    internal bool VerifyPresetLayoutForProbe() =>
        ScaleHost.ActualWidth > 0 && ScaleHost.ActualWidth <= 320 && SpacingHost.ActualWidth == ScaleHost.ActualWidth &&
        _scaleMarks?.Ready == true && _spacingMarks?.Ready == true &&
        _scaleMarks.CenterForProbe(0).X < _scaleMarks.CenterForProbe(1).X &&
        _scaleMarks.CenterForProbe(1).X < _scaleMarks.CenterForProbe(2).X &&
        ScaleMarks.IsHitTestVisible == false && SpacingMarks.IsHitTestVisible == false &&
        _scaleMarks.ThumbAlignedForProbe() && _spacingMarks.ThumbAlignedForProbe();
    internal async Task CaptureSettingsForProbeAsync(string path)
    {
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(Content);
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }

    internal async Task VerifyContentEntryForProbe(bool empty, string evidenceRoot)
    {
        ShowWorkspacePage(false);
        RenderContentLayout();
        if (empty)
        {
            if (ContentManageButton.IsEnabled || ContentKeyboardPad.IsEnabled)
                throw new InvalidOperationException("Empty poster still offers an active item command.");
            return;
        }
        var view = _contentRuntime!.Invoke()!.ReadContentEditor(_view!.SelectedPosterId)!;
        string item = view.Document.Placements.First().ItemId;
        ContentAction((runtime, id) => runtime.SelectContentEditor(id, item));
        await Task.Delay(80);
        RenderContentLayout();
        MenuFlyoutItem Repair() => ((MenuFlyout)ContentKeyboardPad.ContextFlyout).Items
            .OfType<MenuFlyoutItem>().Single(x => x.Text == "修复所选引用…");
        if (((MenuFlyout)ContentKeyboardPad.ContextFlyout).Items.OfType<MenuFlyoutItem>().Any(x =>
            x.Text.Contains("全选引用") || x.Text.Contains("从此海报移除")))
            throw new InvalidOperationException("Retired selection/removal actions still appear in the menu.");
        if (!ContentManageButton.IsEnabled || !Repair().IsEnabled)
            throw new InvalidOperationException("A single real item has no repair entry.");
        if (view.Document.Placements.Count > 1)
        {
            ContentAction((runtime, id) => runtime.SelectAllRealContent(id));
            await Task.Delay(80);
            RenderContentLayout();
            if (Repair().IsEnabled) throw new InvalidOperationException("Batch selection offered a single-item repair.");
            ContentAction((runtime, id) => runtime.SelectContentEditor(id, item));
            await Task.Delay(80);
            RenderContentLayout();
        }
        // Other appearance checks may leave tall folders expanded; a native flyout needs a
        // visible anchor, just as a user would scroll to the selected-item row first.
        UiCTemplateFolder.IsExpanded = LayoutFolder.IsExpanded = false;
        Activate();
        UiCRoot.UpdateLayout();
        ContentManageButton.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        await Task.Delay(180);
        ContentManageButton.Focus(FocusState.Programmatic);
        var openedMenu = ContentKeyboardPad.ContextFlyout;
        bool menuOpened = false, menuClosed = false;
        openedMenu.Opened += (_, _) => menuOpened = true;
        openedMenu.Closed += (_, _) => menuClosed = true;
        ContentManage_Click(ContentManageButton, new RoutedEventArgs());
        var openedAt = DateTime.UtcNow;
        while (!menuOpened || !Repair().IsLoaded)
        {
            if (DateTime.UtcNow - openedAt > TimeSpan.FromSeconds(3)) throw new IOException("Repair flyout did not become ready.");
            await Task.Delay(50);
        }
        await Task.Delay(120);
        var repair = Repair();
        repair.Focus(FocusState.Programmatic);
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(repair) ?? new MenuFlyoutItemAutomationPeer(repair);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        var started = DateTime.UtcNow;
        while (_portableDialog is null)
        {
            if (DateTime.UtcNow - started > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException($"The scoped repair menu did not open its dialog: opened={menuOpened}, closed={menuClosed}, same={ReferenceEquals(openedMenu, ContentKeyboardPad.ContextFlyout)}, enabled={repair.IsEnabled}, busy={_portableBusy}, draft={_settings.Preview?.Setting}, notice={ContentLayoutNotice.Text}, settings={CommonSettingsNotice.Text}.");
            await Task.Delay(50);
        }
        if (_portableDialog.Title as string != "修复所选卡片来源")
            throw new InvalidOperationException("Unexpected repair dialog.");
        await CaptureSettingsForProbeAsync(Path.Combine(evidenceRoot, "repair-entry.png"));
        _portableDialog.Hide();
        started = DateTime.UtcNow;
        while (_portableBusy)
        {
            if (DateTime.UtcNow - started > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException("Cancelling repair did not release the UI owner.");
            await Task.Delay(50);
        }
        RenderContentLayout();
        await CaptureSettingsForProbeAsync(Path.Combine(evidenceRoot, "content-entry.png"));
    }
}
