namespace DesktopUp.Application;

// Detached data draft; no control-center widgets, persistence or timer ownership here.
public sealed record PosterAutoPostureDraft(PosterSurfaceProjection Basis, bool Enabled, int LeaveDelaySeconds)
{
    public static PosterAutoPostureDraft Open(PosterSurfaceProjection target) =>
        new(target, target.Poster.AutoCollapseEnabled, target.Poster.AutoCollapseDelaySeconds);

    public PosterStateCommand ToCommand(string requestId) => new(requestId, StateCommandSource.ControlCenter,
        PosterStateCommandKind.SetAutoPosture, Basis.Revision, Basis.Poster.PosterId, Basis.Poster with
        {
            AutoCollapseEnabled = Enabled, AutoCollapseDelaySeconds = LeaveDelaySeconds
        }) { ReadSet = StateReadSet.ForPoster(Basis.Poster.PosterId, Basis.EntityVersion) };
}
