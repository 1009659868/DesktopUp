using DesktopUp.Application;

namespace DesktopUp;
internal sealed partial class PosterVisualWindow
{
    private ContentSelection _contentSelection = ContentSelection.Empty;
    private string[] ContentSelectionOrder() => _contentSnapshot is { } snapshot
        ? snapshot.Items.OrderBy(p => p.ReadOrder).Select(p => p.ItemId).Distinct().ToArray()
        : ContentDocument.Placements.Select(p => p.ItemId).ToArray();
    private string[] SelectedContentIds() => IsRealContent
        ? _contentSelection.Prune(ContentSelectionOrder()).Ids.ToArray()
        : _selectedContentItem is { } id ? [id] : [];
    private void SetContentSelection(string? id, bool control = false, bool shift = false)
    {
        _contentSelection = _contentSelection.Select(ContentSelectionOrder(), id, control, shift);
        _selectedContentItem = IsRealContent ? _contentSelection.Focus : id;
    }
    internal string? SelectAllRealContent(bool clear = false)
    {
        if (!IsRealContent) return "当前仍为 DEMO。";
        if (_disposed || _contentPresentationSuspended || HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!) || spatial.HasCenterDraft(PosterId!))
            return "请先完成当前操作。";
        var order = ContentSelectionOrder();
        _contentSelection = clear ? ContentSelection.Empty : new(order, order.FirstOrDefault(), order.LastOrDefault()) { ShowHighlight = order.Length > 0 };
        _selectedContentItem = _contentSelection.Focus;
        _contentActivation.Reset();
        RefreshContentSurface(); spatial.NotifyContentEditorChanged(PosterId); return null;
    }
}
