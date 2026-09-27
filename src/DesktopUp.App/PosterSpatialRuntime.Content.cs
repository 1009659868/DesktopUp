using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal event Action<string>? ContentEditorChanged;
    internal void NotifyContentEditorChanged(string? id) { if(id is not null) ContentEditorChanged?.Invoke(id); }
    internal void NotifyCenterContentInteractionChanged()
    {
        // Each window wakes only if its own pending proposal was blocked by a center draft.
        // Merely changing center focus/settings does not enqueue reflows for other posters.
        foreach(var window in _windows.Values) window.NotifyCenterContentInteractionChanged();
    }
    internal ContentLayoutEditorView? ReadContentEditor(string? id) => id is not null && !_suspended && _windows.TryGetValue(id,out var window)
        ? window.ReadContentEditor() : null;
    internal ContentInteractionFeedback? PreviewContentEditor(string id, ContentLayoutEdit edit) =>
        !_suspended && !IsSpatialBusy && _windows.TryGetValue(id, out var window) ? window.PreviewContentEdit(edit) : null;
    internal string? ApplyContentEditor(string id,ContentLayoutEdit edit)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未显示或正在交互，请稍后重试。";
        return window.EditContent(edit);
    }
    internal string? ApplyContentEditorFromCenter(string id, ContentLayoutEdit edit, long expectedEntityVersion, long expectedDocumentVersion)
    {
        if (_suspended || IsSpatialBusy || !_windows.TryGetValue(id, out var window) ||
            !OwnsCenterContentDraft(id, expectedEntityVersion, expectedDocumentVersion)) return "当前内容草稿或海报状态已变化。";
        return window.EditContent(edit, expectedEntityVersion, expectedDocumentVersion);
    }
    internal string? ActivateContentEditor(string id, bool isRepeat) =>
        !_suspended && _windows.TryGetValue(id, out var window) ? window.ActivateSelectedContent(isRepeat) : "海报尚未就绪。";
    internal void ReleaseContentActivationKey(string id)
    { if (_windows.TryGetValue(id, out var window)) window.ReleaseContentActivationKey(); }
    internal string? SelectContentEditor(string id,string? itemId)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未就绪。";
        return window.SelectContentItem(itemId);
    }
    internal string? SetContentEditing(string id,bool editing)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未就绪。";
        return window.SetEditing(editing)?null:"未能进入编辑模式。";
    }
    internal string? ApplyContentSample(string id,string sample)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未就绪。";
        return window.UseContentSample(sample);
    }
    internal string? UndoContentEditor(string id,bool redo)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未就绪。";
        return window.UndoContent(redo);
    }
    internal string? NudgeContentEditor(string id,int x,int y,bool resize)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未就绪。";
        return window.NudgeContent(x,y,resize);
    }
    internal string? SetContentFlowAxis(string id,ContentFlowAxis axis)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未显示或正在交互，请稍后重试。";
        return window.SetContentFlowAxis(axis);
    }
    internal string? SetContentLoopScroll(string id,bool enabled)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未显示或正在交互，请稍后重试。";
        return window.SetContentLoopScroll(enabled);
    }
}
