namespace DesktopUp.Application;

// A separate, narrow port. General Execute still rejects System callers.
public interface IAutomaticPostureCommitter
{
    StateCommandResult ExecuteAutomaticPosture(PosterStateCommand command);
}

public static class AutomaticPostureCommitPolicy
{
    public static StateMutationPlan Prepare(PosterStateCommand command, DesktopStateSnapshot state)
    {
        var current = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (command.Source != StateCommandSource.System || command.Kind != PosterStateCommandKind.SetCollapsed ||
            command.SpatialGesture is not null || command.SpatialPosture is not { UseContinuity: true, AllowPush: true } ||
            command.ReadSet?.CatalogVersion is null || current is not { AutoCollapseEnabled: true, IsVisible: true } ||
            command.ReadSet.Posters.Count != state.Catalog.Posters.Count ||
            state.Catalog.Posters.Any(p => !command.ReadSet.Posters.ContainsKey(p.Poster.PosterId)))
            return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "Automatic posture requires an enabled visible target and a full spatial CAS transaction.");
        return StateCommandPolicy.Prepare(command, state);
    }
}
