using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal event Action<string>? ContentEditorChanged;
    internal void NotifyContentEditorChanged(string? id) { if(id is not null) ContentEditorChanged?.Invoke(id); }
    internal ContentLayoutEditorView? ReadContentEditor(string? id) => id is not null && !_suspended && _windows.TryGetValue(id,out var window)
        ? window.ReadContentEditor() : null;
    internal string? ApplyContentEditor(string id,ContentLayoutEdit edit)
    {
        if(_suspended || IsSpatialBusy || !_windows.TryGetValue(id,out var window)) return "海报尚未显示或正在交互，请稍后重试。";
        return window.EditContent(edit);
    }
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
    internal void CancelContentEditors() { foreach(var window in _windows.Values) window.CancelContentEditingFromCenter(); }
}
