using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Ordinary content updates never paint ahead of native admission. This also removes the
    // previous duplicate draw in ApplyFrame. Geometry batches keep their existing readback/rollback.
    private bool ApplyContentFrame(DesktopPixelRect bounds, PosterInputLayoutSnapshot? input = null)
    {
        input ??= BuildInputLayout(bounds);
        var frame = _contentLayout.Current;
        if (frame is null || input.ContentSnapshotGeneration != frame.Snapshot.ViewportGeneration ||
            _inputIsland?.ApplyLayout(bounds, input) != true)
        {
            _inputIsland?.SuspendRouting();
            return false;
        }
        var previous = Presenter.Frame;
        bool painted = false;
        try
        {
            DrawContentSnapshot(frame.Snapshot, frame.Viewport);
            painted = true;
            UpdateScene();
            PublishContentSpace(frame.Space);
            return true;
        }
        finally
        {
            if (!painted)
            {
                _inputIsland?.SuspendRouting();
                if (previous is not null && _itemsGrid is not null) Presenter.Present(_itemsGrid, previous);
                _contentLayout.Invalidate();
            }
        }
    }
}
