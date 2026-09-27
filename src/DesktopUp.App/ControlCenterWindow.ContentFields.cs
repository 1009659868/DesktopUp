using System.Globalization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using global::Windows.System;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private enum ContentFieldGroup { Span, Order, Position }
    // Raw center-only input, bound to the exact committed basis. It never substitutes for the
    // document or solver and does not preview a half-entered pair onto the poster.
    private sealed record ContentFieldDraft(string PosterId, string ItemId, long EntityVersion,
        long DocumentVersion, ContentFieldGroup Group, string First, string Second);
    private ContentFieldDraft? _contentFieldDraft;
    private string? _contentFieldNotice;
    private bool _renderingContentFields, _contentFieldApplying;
    internal bool IsContentFieldDraftActive => _contentFieldDraft is not null;
    internal bool HasContentFieldDraft(string? id) => id is not null && _contentFieldDraft?.PosterId == id;
    internal bool OwnsContentFieldDraft(string id, long entityVersion, long documentVersion) =>
        _contentFieldApplying && !OtherContentOwner && _contentFieldDraft is { } draft &&
        draft.PosterId == id && draft.EntityVersion == entityVersion && draft.DocumentVersion == documentVersion;
    private void ClearContentFieldDraft()
    {
        bool hadDraft = _contentFieldDraft is not null;
        _contentFieldDraft = null;
        _contentFieldNotice = null;
        if (hadDraft) SettingsInteractionChanged?.Invoke();
    }
    internal void CancelContentFieldDraft()
    {
        if (_contentFieldApplying) return;
        ClearContentFieldDraft();
        if (!_editingClosed) { RenderContentLayout(); RenderEditing(); RenderSettings(); }
    }
    private static ContentFieldGroup? ContentFieldGroupOf(object sender) => ((sender as FrameworkElement)?.Tag as string) switch
    { "span" => ContentFieldGroup.Span, "order" => ContentFieldGroup.Order, "position" => ContentFieldGroup.Position, _ => null };
    private (TextBox First, TextBox? Second) ContentFieldBoxes(ContentFieldGroup group) => group switch
    {
        ContentFieldGroup.Span => (ContentColumns, ContentRows),
        ContentFieldGroup.Order => (ContentOrder, null),
        _ => (ContentColumnPosition, ContentRowPosition)
    };
    private void ContentField_TextChanging(TextBox sender, TextBoxTextChangingEventArgs e)
    {
        if (_renderingContent || _renderingContentFields || _editingClosed || _contentFieldApplying || ContentFieldGroupOf(sender) is not { } group) return;
        if (_contentFieldDraft is { } other && other.Group != group)
        {
            _contentFieldNotice = "请先应用或取消当前字段组；原文已保留。";
            QueueContentRefresh(); return;
        }
        if (OtherContentOwner || _view?.SelectedPosterId is not { } id || DesktopInteractionConflicts(id) ||
            _contentRuntime?.Invoke()?.ReadContentEditor(id) is not { CanEdit: true, SelectedItemId: { } item } view)
        {
            _contentFieldNotice = "当前操作尚未结束，卡片字段修改未开始。";
            QueueContentRefresh(); return;
        }
        var placement = view.Document.Placements.FirstOrDefault(p => p.ItemId == item);
        if (placement is null || view.SelectedSlot is null ||
            group == ContentFieldGroup.Order && (view.Document.Mode == ContentLayoutMode.Manual || placement.FixedPosition) ||
            group == ContentFieldGroup.Position && view.Document.Mode != ContentLayoutMode.Manual)
        {
            _contentFieldNotice = "当前卡片不支持这一字段组；原排布已保留。";
            QueueContentRefresh(); return;
        }
        var boxes = ContentFieldBoxes(group);
        bool begins = _contentFieldDraft is null;
        _contentFieldDraft = _contentFieldDraft is { } existing
            ? existing with { First = boxes.First.Text, Second = boxes.Second?.Text ?? "" }
            : new(id, item, view.EntityVersion, view.Document.DocumentVersion, group, boxes.First.Text, boxes.Second?.Text ?? "");
        _contentFieldNotice = DescribeContentFieldDraft(_contentFieldDraft, view);
        // TextChanging captures ownership synchronously. TextChanged/queued render may be later;
        // another control in this dispatcher turn must already see the raw (possibly invalid) draft.
        if (begins) SettingsInteractionChanged?.Invoke();
        QueueContentRefresh();
    }
    private void ContentField_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { e.Handled = true; CancelContentFieldDraft(); }
        else if (e.Key == VirtualKey.Enter && ContentFieldGroupOf(sender) is { } group)
        { e.Handled = true; if (!e.KeyStatus.WasKeyDown) ApplyContentFields(group); }
    }
    private void ContentFieldApply_Click(object sender, RoutedEventArgs e)
    { if (ContentFieldGroupOf(sender) is { } group) ApplyContentFields(group); }
    private void ContentFieldCancel_Click(object sender, RoutedEventArgs e)
    { if (ContentFieldGroupOf(sender) == _contentFieldDraft?.Group) CancelContentFieldDraft(); }
    private void ApplyContentFields(ContentFieldGroup group)
    {
        if (_contentFieldApplying || _editingClosed || _contentFieldDraft is not { } draft || draft.Group != group) return;
        if (OtherContentOwner || _view?.SelectedPosterId != draft.PosterId ||
            _contentRuntime?.Invoke() is not { } runtime || runtime.ReadContentEditor(draft.PosterId) is not { } view)
        { _contentFieldNotice = "目标或当前操作已变化；草稿保留，请取消后重试。"; RenderContentLayout(); return; }
        if (ValidateContentFields(draft, view, out var edit) is { } invalid)
        { _contentFieldNotice = invalid; RenderContentLayout(); return; }
        _contentFieldApplying = true;
        string? error;
        try
        {
            // Keep this draft's ownership until the existing command/CAS path returns. The runtime
            // authorizes only this captured owner and still checks the actual current basis.
            error = runtime.ApplyContentEditorFromCenter(draft.PosterId, edit!, draft.EntityVersion, draft.DocumentVersion);
        }
        finally { _contentFieldApplying = false; }
        if (error is null) ClearContentFieldDraft();
        else _contentFieldNotice = error;
        RenderContentLayout(); RenderEditing(); RenderSettings();
    }
    private static string? ValidateContentFields(ContentFieldDraft draft, ContentLayoutEditorView view, out ContentLayoutEdit? edit)
    {
        edit = null;
        if (view.EntityVersion != draft.EntityVersion || view.Document.DocumentVersion != draft.DocumentVersion ||
            view.SelectedItemId != draft.ItemId || view.Document.PosterId != draft.PosterId)
            return "草稿基准或卡片选择已变化；原文保留，请取消后重新输入。";
        if (!view.CanEdit) return "当前排布不可编辑；原文保留。";
        bool Integer(string text, int maximum, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1 && value <= maximum;
        if (draft.Group == ContentFieldGroup.Span)
        {
            if (!Integer(draft.First, ContentLayoutPolicy.MaximumSpan, out int columns) || !Integer(draft.Second, ContentLayoutPolicy.MaximumSpan, out int rows))
                return "列跨度和行跨度都需要 1～32 的整数；未应用的原文已保留。";
            edit = new(ContentLayoutEditKind.Resize, draft.ItemId) { ColumnSpan = columns, RowSpan = rows };
        }
        else if (draft.Group == ContentFieldGroup.Order)
        {
            if (!Integer(draft.First, view.Document.Placements.Count, out int order))
                return $"顺序位需要 1～{view.Document.Placements.Count} 的整数；未应用的原文已保留。";
            edit = new(ContentLayoutEditKind.Move, draft.ItemId) { TargetReadOrder = order - 1 };
        }
        else
        {
            if (!Integer(draft.First, ContentLayoutPolicy.MaximumRow + 1, out int column) || !Integer(draft.Second, ContentLayoutPolicy.MaximumRow + 1, out int row))
                return "列和行需要从 1 开始的整数（上限 65536）；是否可放置由当前网格校验。";
            edit = new(ContentLayoutEditKind.Move, draft.ItemId) { Column = column - 1, Row = row - 1 };
        }
        return null;
    }
    private string DescribeContentFieldDraft(ContentFieldDraft draft, ContentLayoutEditorView view)
    {
        if (ValidateContentFields(draft, view, out var edit) is { } invalid) return invalid;
        // Read-only impact from the complete production snapshot and the existing editor result.
        var feedback = _contentRuntime?.Invoke()?.PreviewContentEditor(draft.PosterId, edit!);
        if (feedback is null) return "当前暂不能预览影响；原文保留，请稍后重试或取消。";
        string message = feedback.Message.Replace("松手", "应用后", StringComparison.Ordinal);
        return feedback.State == ContentFeedbackState.Rejected ? "当前无法应用 · " + message : "尚未应用 · " + message;
    }
    private void RenderContentFields(ContentLayoutEditorView? view)
    {
        if (_renderingContentFields) return;
        _renderingContentFields = true;
        try
        {
            var placement = view?.Document.Placements.FirstOrDefault(p => p.ItemId == view.SelectedItemId);
            var slot = view?.SelectedSlot;
            bool ready = view?.CanEdit == true && placement is not null && slot is not null && !OtherContentOwner && !_contentFieldApplying;
            bool automatic = view?.Document.Mode != ContentLayoutMode.Manual;
            bool orderAvailable = automatic && placement is { FixedPosition: false };
            ContentOrderGroup.Visibility = automatic ? Visibility.Visible : Visibility.Collapsed;
            ContentPositionGroup.Visibility = automatic ? Visibility.Collapsed : Visibility.Visible;
            SetContentFieldText(ContentFieldGroup.Span, slot?.ColumnSpan.ToString(CultureInfo.InvariantCulture) ?? "", slot?.RowSpan.ToString(CultureInfo.InvariantCulture) ?? "");
            SetContentFieldText(ContentFieldGroup.Order, slot is null ? "" : (slot.ReadOrder + 1).ToString(CultureInfo.InvariantCulture), "");
            SetContentFieldText(ContentFieldGroup.Position, slot is null ? "" : (slot.Column - slot.RegionColumn + 1).ToString(CultureInfo.InvariantCulture),
                slot is null ? "" : (slot.Row - slot.RegionRow + 1).ToString(CultureInfo.InvariantCulture));
            bool GroupReady(ContentFieldGroup group) => ready && (_contentFieldDraft is null || _contentFieldDraft.Group == group);
            ContentColumns.IsEnabled = ContentRows.IsEnabled = GroupReady(ContentFieldGroup.Span);
            ContentOrder.IsEnabled = GroupReady(ContentFieldGroup.Order) && orderAvailable;
            ContentColumnPosition.IsEnabled = ContentRowPosition.IsEnabled = GroupReady(ContentFieldGroup.Position) && !automatic;
            ContentSpanApply.IsEnabled = ContentSpanCancel.IsEnabled = _contentFieldDraft?.Group == ContentFieldGroup.Span && !_contentFieldApplying;
            ContentOrderApply.IsEnabled = ContentOrderCancel.IsEnabled = _contentFieldDraft?.Group == ContentFieldGroup.Order && !_contentFieldApplying;
            ContentPositionApply.IsEnabled = ContentPositionCancel.IsEnabled = _contentFieldDraft?.Group == ContentFieldGroup.Position && !_contentFieldApplying;
            ContentOrderHint.Text = placement?.FixedPosition == true ? "旧硬位置保持兼容，不转换成顺序图钉。" :
                $"填写 1～{view?.Document.Placements.Count ?? 0}；应用会建立顺序图钉，已占用的图钉位向后找空位。";
            ContentFieldStatus.Text = _contentFieldDraft is { } draft
                ? view is null ? "当前目标不可用；未应用的原文保留，可取消。" : DescribeContentFieldDraft(draft, view)
                : "同组一起应用 · Esc 取消";
        }
        finally { _renderingContentFields = false; }
    }
    private void SetContentFieldText(ContentFieldGroup group, string first, string second)
    {
        if (_contentFieldDraft is { } draft && draft.Group == group) { first = draft.First; second = draft.Second; }
        var boxes = ContentFieldBoxes(group);
        if (boxes.First.Text != first) boxes.First.Text = first;
        if (boxes.Second is { } other && other.Text != second) other.Text = second;
    }
}
