using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal void TrackContentOperation(Task operation) => _contentOperations.Track(operation);
    internal async Task WaitForContentOperationsAsync()
    {
        await _contentOperations.DrainAsync();
        _contentOperations.Dispose();
    }
    internal string? RevealContentEditor(string id) => _windows.TryGetValue(id, out var window) ? window.RevealSelectedContent() : "请先显示海报。";
    internal string? SelectAllRealContent(string id, bool clear = false) => _windows.TryGetValue(id, out var window) ? window.SelectAllRealContent(clear) : "请先显示海报。";
    internal string? RefreshRealContentEditor(string id) => _windows.TryGetValue(id, out var window) ? window.RefreshRealContent() : "请先显示海报。";
    internal string? DetachContent(ContentDetachCommand command)
    {
        // The center confirmation itself owns the portable-dialog protection. Poster input
        // must respect it; the confirmed center command still passes full relation/poster CAS.
        if (_suspended || IsPosterSpatialBusy(command.PosterId) ||
            HasCenterDraft(command.PosterId) && command.Source != StateCommandSource.ControlCenter ||
            _windows.TryGetValue(command.PosterId, out var window) && window.HasContentGesture) return "请先完成当前海报交互。";
        var result = commands.Execute(command);
        return result.Succeeded ? null : result.Error ?? "未移除，请刷新后重试。";
    }
}
