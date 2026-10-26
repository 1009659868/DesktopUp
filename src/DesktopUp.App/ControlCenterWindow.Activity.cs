using System.Globalization;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private ContentDialog? _activityDialog;
    private string? _activityPosterId;
    private ContentLayoutEditorView? _activityBasis;
    private bool _activityApplying;

    // The existing center command path accepts only the owner currently committing this exact
    // draft. Keeping ownership through the synchronous CAS prevents a deferred heat drain from
    // becoming a second author between validation and persistence.
    private bool OwnsActivityDraft(string id, long entityVersion, long documentVersion) =>
        _activityApplying && _activityDialog is not null && _activityPosterId == id &&
        _activityBasis is { } basis && basis.EntityVersion == entityVersion && basis.Document.DocumentVersion == documentVersion &&
        ActivityDraftIsCurrent(id, basis);

    private bool ActivityDraftIsCurrent(string id, ContentLayoutEditorView basis) =>
        !_editingClosed && _view?.SelectedPosterId == id && _lampColorFlyout is null && !_portableBusy &&
        !_settings.ProtectsInteraction && !_editor.ProtectsInteraction && _catalogDialog is null && _removalDialog is null &&
        !IsContentFieldDraftActive && !DesktopInteractionConflicts(id) &&
        _contentRuntime?.Invoke()?.ReadContentEditor(id) is { CanEdit: true } current &&
        current.Document.PosterId == basis.Document.PosterId && current.EntityVersion == basis.EntityVersion &&
        current.Document.DocumentVersion == basis.Document.DocumentVersion && current.Document.ItemSetVersion == basis.Document.ItemSetVersion &&
        current.SpaceEpoch == basis.SpaceEpoch && current.SpaceSession == basis.SpaceSession;

    private async void ActivitySettings_Click(object sender, RoutedEventArgs e)
    {
        if (_editingClosed || OtherContentOwner || IsContentFieldDraftActive || _templateReferenceOpen || Content.XamlRoot is null ||
            _view?.SelectedPosterId is not { } id || DesktopInteractionConflicts(id) ||
            _contentRuntime?.Invoke()?.ReadContentEditor(id) is not { CanEdit: true } basis) return;
        var initial = basis.Document.Activity;
        var registered = _contentRuntime.Invoke()?.ActivityModels ?? [];
        var model = new ComboBox { Header = "行为模型", HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(model, "ActivityModel");
        foreach (var descriptor in registered)
        {
            var choice = new ComboBoxItem { Content = descriptor.Name, Tag = descriptor };
            model.Items.Add(choice);
            if (descriptor.Id == initial.ModelId && descriptor.Version == initial.ModelVersion) model.SelectedItem = choice;
        }
        if (model.SelectedItem is null)
        {
            var unavailable = new ComboBoxItem { Content = "不可用 · " + initial.ModelId, Tag = null };
            model.Items.Add(unavailable); model.SelectedItem = unavailable;
        }
        var transitions = new ToggleSwitch { Header = "模型过渡效果", IsOn = initial.AllowTransitions,
            OnContent = "允许", OffContent = "关闭" };
        AutomationProperties.SetAutomationId(transitions, "ActivityTransitions");
        var minimum = ActivityField("最小倍率（0.5～3×）", "ActivityMinimumScale", initial.MinimumScale, .5, 3, .05);
        var maximum = ActivityField("最大倍率（0.5～3×）", "ActivityMaximumScale", initial.MaximumScale, .5, 3, .05);
        var baseline = ActivityField("默认倍率", "ActivityDefaultScale", initial.DefaultScale, .5, 3, .05);
        var smoothing = ActivityField("平滑强度（默认 20）", "ActivitySmoothingStrength", initial.SmoothingStrength, 0, 10000, 1);
        var enabled = new ToggleSwitch { Header = "允许热度调整内容大小", OnContent = "开启", OffContent = "关闭", IsOn = basis.Document.AllowHeatDrivenSize };
        AutomationProperties.SetAutomationId(enabled, "ActivityScaleEnabled");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(error, "ActivitySettingsError");
        AutomationProperties.SetLiveSetting(error, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var body = new StackPanel { Spacing = 14, MinWidth = 280, MaxWidth = 440 };
        body.Children.Add(new TextBlock { Text = "模型建议当前海报的排序、大小和布局。切换不清空使用记录，手动编排与图钉优先。最近使用模型采用条带布局及短淡入。", TextWrapping = TextWrapping.Wrap, Opacity = .75 });
        foreach (var element in new FrameworkElement[] { model, transitions, enabled, minimum, maximum, baseline, smoothing,
            new TextBlock { Text = "无使用记录时采用默认倍率。平滑强度越大，少量使用时大小差异越小；设为 0 关闭平滑。手动排布与手工尺寸优先。", TextWrapping = TextWrapping.Wrap }, error })
            body.Children.Add(element);
        var scroller = new ScrollViewer { Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(180, Math.Min(520, UiCRoot.ActualHeight - 180)) };
        void ShowError(string message)
        {
            error.Text = message;
            // In a short center window, feedback must be visible without the user guessing
            // that an unchanged dialog has an error below the current scroll position.
            scroller.UpdateLayout();
            scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true);
        }
        var dialog = PaperDialog("当前海报 · 热度设置", scroller, "取消", "保存");
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.Resources["ContentDialogMinWidth"] = Math.Max(300d, Math.Min(420d, UiCRoot.ActualWidth - 60));
        dialog.Resources["ContentDialogMaxWidth"] = Math.Max(320d, Math.Min(560d, UiCRoot.ActualWidth - 40));
        AutomationProperties.SetAutomationId(dialog, "ActivitySettingsDialog");
        dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            // Read the displayed draft, including unfinished/invalid text, rather than accepting
            // a NumberBox's last valid Value while a different string is still visible.
            double Number(NumberBox field) => double.TryParse(field.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out double value) && double.IsFinite(value) ? value : double.NaN;
            if ((model.SelectedItem as ComboBoxItem)?.Tag is not ContentModelDescriptor descriptor)
            { ShowError("请选择一个可用模型；当前画面和使用记录保持不变。"); return; }
            var selected = new ContentActivityOptions(Number(minimum), Number(maximum), Number(baseline), Number(smoothing))
            { ModelId = descriptor.Id, ModelVersion = descriptor.Version, ParameterVersion = descriptor.ParameterVersion,
                AllowTransitions = transitions.IsOn };
            if (selected.Validate() is not null)
            {
                ShowError("倍率需在 0.5～3 之间，且最小 ≤ 默认 ≤ 最大；平滑强度需在 0～10000 之间。请填写有效数字。");
                return;
            }
            if (!ActivityDraftIsCurrent(id, basis) || _contentRuntime?.Invoke() is not { } runtime)
            {
                ShowError("海报或可用空间已变化，请取消后重新打开设置。");
                return;
            }
            _activityApplying = true;
            string? failure;
            try { failure = runtime.ApplyContentEditorFromCenter(id,
                new(ContentLayoutEditKind.Activity) { Activity = selected, Enabled = enabled.IsOn }, basis.EntityVersion, basis.Document.DocumentVersion); }
            finally { _activityApplying = false; }
            if (failure is not null) { ShowError(failure); return; }
            args.Cancel = false;
        };
        _activityDialog = dialog; _activityPosterId = id; _activityBasis = basis;
        SetActivityNotice(null);
        SettingsInteractionChanged?.Invoke(); RenderContentLayout(); RenderEditing(); RenderSettings();
        try
        {
            var result = await dialog.ShowAsync();
            if (!_editingClosed && result == ContentDialogResult.Primary && _view?.SelectedPosterId == id)
                SetActivityNotice("已保存 · 仅当前海报");
        }
        catch (InvalidOperationException) { if (!_editingClosed) SetActivityNotice("暂时无法打开热度设置，请先关闭其他对话框。", warning: true); }
        catch (System.Runtime.InteropServices.COMException) { if (!_editingClosed) SetActivityNotice("热度设置窗口未能完成操作，请重新打开。", warning: true); }
        finally
        {
            _activityDialog = null; _activityPosterId = null; _activityBasis = null; _activityApplying = false;
            if (ReferenceEquals(_activeTemplateDialog, dialog)) _activeTemplateDialog = null;
            if (!_editingClosed) { RenderContentLayout(); RenderEditing(); RenderSettings(); }
            SettingsInteractionChanged?.Invoke();
        }
    }

    private void SetActivityNotice(string? message, bool warning = false)
    {
        ActivitySettingsNotice.IsOpen = message is not null;
        ActivitySettingsNotice.Severity = warning ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        ActivitySettingsNotice.Message = message ?? "";
    }

    private static NumberBox ActivityField(string caption, string automationId, double value, double minimum, double maximum, double step)
    {
        var field = new NumberBox { Header = caption, Value = value, Minimum = minimum, Maximum = maximum, SmallChange = step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, ValidationMode = NumberBoxValidationMode.Disabled };
        AutomationProperties.SetAutomationId(field, automationId);
        return field;
    }
}
