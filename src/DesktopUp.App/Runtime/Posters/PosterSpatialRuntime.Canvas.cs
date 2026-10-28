using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime : ICanvasPreviewHost
{
    internal CanvasContentSnapshot? ReadCanvasContent(string id) =>
        _windows.TryGetValue(id, out var window) ? window.ReadCanvasContent() : null;
    bool ICanvasPreviewHost.Preview(PosterStateCommand command)
    {
        if (_suspended || IsPosterSpatialBusy(command.PosterId) ||
            !state.TryGetCurrent(out var current) || current!.PostersById.GetValueOrDefault(command.PosterId) is not { } target ||
            target.EntityVersion != command.ReadSet?.Posters.GetValueOrDefault(command.PosterId) ||
            target.Poster.IsLocked || target.Poster.IsCollapsed || !target.IsEffectivelyVisible ||
            !_windows.TryGetValue(command.PosterId, out var window) || window.CenterPresentationBaseline is not { } bounds) return false;
        try
        {
            if (window.PreviewCenterPresentation(command, bounds) && window.CanvasPresentationReady) return true;
            window.ClearCenterPresentation(); return false;
        }
        catch { window.ClearCenterPresentation(); throw; }
    }
    void ICanvasPreviewHost.Restore(string posterId)
    {
        if (_windows.TryGetValue(posterId, out var window)) window.ClearCenterPresentation();
    }
}
