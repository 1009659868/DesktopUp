namespace DesktopUp.Application;

// A current right-button hit, not a left-button capture or an activation intent.
// The consumer must still check its current content generation before opening a menu.
public sealed record PosterContentContextRequest(PosterInputHit Hit, PointerScreenPoint ScreenPoint)
{
    // Absent for older diagnostic callers; never infer a pin action from release alone.
    public PointerScreenPoint? PressScreenPoint { get; init; }
    public PosterInputSessionKey Session => Hit.Session;
    public string PosterId => Hit.Session.PosterId;
    public string ContentItemId => Hit.Region.ContentItemId!;
    public long LayoutRevision => Hit.LayoutRevision;
    public long ContentSnapshotGeneration => Hit.Region.ContentSnapshotGeneration;
    public DesktopPixelRect Frame => Hit.Frame;
}

public sealed class PosterContentContextEventArgs(PosterContentContextRequest request) : EventArgs
{
    public PosterContentContextRequest Request { get; } = request;
    public bool Handled { get; set; }
}
