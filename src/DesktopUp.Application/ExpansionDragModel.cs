using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ExpansionDragPhase { Expanding, ExpandingAndDragging, Dragging, AwaitingCommit, Completed, Cancelled }
public enum ExpansionDragEventKind { PointerActivity, BeginDrag, ReleaseDrag, ExpansionFinished, CommitSucceeded, Invalidate }
public enum ExpansionDragMotion { Move, Grow }
public sealed record ExpansionDragIdentity(string OperationId, PosterInputSessionKey Session, SpatialEnvironment Environment);
public sealed record ExpansionDragEvent(ExpansionDragIdentity Identity, long Sequence, ExpansionDragEventKind Kind,
    Guid? Capture = null, bool ThresholdReached = false);
public sealed record ExpansionDragState(ExpansionDragIdentity Identity, long Sequence, ExpansionDragPhase Phase,
    SpatialSnapshot Presented, int MinimumExpandedHeight, Guid? Capture = null, bool UserAdjusted = false);
public sealed record ExpansionDragStep(ExpansionDragState State, bool RequestCommit = false, bool Ignored = false);

// Pure lifecycle and presented-frame safety gate. Not a command endpoint or a continuity
// solver: runtime integration additionally requires a replayable spatial/CAS proof.
public static class ExpansionDragModel
{
    public static ExpansionDragState Start(ExpansionDragIdentity identity, SpatialSnapshot presented, int minimumExpandedHeight)
    {
        var source = presented.Posters.FirstOrDefault(p => p.PosterId == identity.Session.PosterId);
        if (string.IsNullOrWhiteSpace(identity.OperationId) || identity.Session.Epoch < 1 ||
            identity.Environment != presented.Environment || source is not { IsVisible: true, IsCollapsed: true } ||
            minimumExpandedHeight < 1 || minimumExpandedHeight > source.ExpandedHeight ||
            presented.Posters.Count > 256 || PosterSpatialSolver.ValidateLayout(presented).Count != 0)
            throw new ArgumentException("Invalid expansion identity, source or presented scene.");
        return new(identity, 0, ExpansionDragPhase.Expanding, presented, minimumExpandedHeight);
    }

    public static ExpansionDragStep Advance(ExpansionDragState state, ExpansionDragEvent input)
    {
        if (input.Identity != state.Identity || input.Sequence <= state.Sequence || Terminal(state.Phase))
            return new(state, Ignored: true);
        if (!Enum.IsDefined(input.Kind)) throw new ArgumentException("Unknown combination event.");
        var next = state with { Sequence = input.Sequence };
        if (input.Kind == ExpansionDragEventKind.Invalidate)
            return new(next with { Phase = ExpansionDragPhase.Cancelled, Capture = null });
        switch (input.Kind)
        {
            case ExpansionDragEventKind.BeginDrag when state.Phase == ExpansionDragPhase.Expanding &&
                input.ThresholdReached && input.Capture is { } capture && capture != Guid.Empty &&
                !Source(state).IsLocked:
                next = next with { Phase = ExpansionDragPhase.ExpandingAndDragging, Capture = capture };
                break;
            case ExpansionDragEventKind.ReleaseDrag when input.Capture is not null && input.Capture == state.Capture:
                next = next with { Capture = null, Phase = state.Phase switch
                {
                    ExpansionDragPhase.ExpandingAndDragging => ExpansionDragPhase.Expanding,
                    ExpansionDragPhase.Dragging => ExpansionDragPhase.AwaitingCommit,
                    _ => state.Phase
                } };
                break;
            case ExpansionDragEventKind.ExpansionFinished when Source(state).Bounds.Height >= state.MinimumExpandedHeight:
                next = next with { Phase = state.Phase switch
                {
                    ExpansionDragPhase.Expanding => ExpansionDragPhase.AwaitingCommit,
                    ExpansionDragPhase.ExpandingAndDragging => ExpansionDragPhase.Dragging,
                    _ => state.Phase
                } };
                break;
            case ExpansionDragEventKind.CommitSucceeded when state.Phase == ExpansionDragPhase.AwaitingCommit:
                next = next with { Phase = ExpansionDragPhase.Completed };
                break;
        }
        return new(next, state.Phase != ExpansionDragPhase.AwaitingCommit && next.Phase == ExpansionDragPhase.AwaitingCommit);
    }

    // Check before native submission; call AcceptPresented only AFTER that exact batch succeeds.
    // The proposal alone must never advance the source, its neighbors or the lifecycle.
    public static string? CheckFrame(ExpansionDragState state, ExpansionDragMotion motion, SpatialBatchFrame frame)
    {
        if (!Enum.IsDefined(motion) || Terminal(state.Phase) || state.Phase == ExpansionDragPhase.AwaitingCommit)
            return "inactive-or-invalid-motion";
        if (motion == ExpansionDragMotion.Move && state.Phase is not
            (ExpansionDragPhase.ExpandingAndDragging or ExpansionDragPhase.Dragging)) return "capture-not-owned";
        if (motion == ExpansionDragMotion.Grow && state.Phase is not
            (ExpansionDragPhase.Expanding or ExpansionDragPhase.ExpandingAndDragging)) return "expansion-already-finished";
        if (frame.Changes.Count == 0 || frame.Changes.Count > state.Presented.Posters.Count ||
            frame.Changes.Select(c => c.PosterId).Distinct(StringComparer.Ordinal).Count() != frame.Changes.Count)
            return "invalid-batch";
        var positions = state.Presented.Posters.ToDictionary(p => p.PosterId, p => p.Bounds, StringComparer.Ordinal);
        var source = Source(state);
        foreach (var leg in frame.Changes)
        {
            var poster = state.Presented.Posters.FirstOrDefault(p => p.PosterId == leg.PosterId);
            if (poster is null || !poster.IsVisible || leg.From != poster.Bounds || !leg.To.IsValid ||
                !state.Identity.Environment.WorkArea.Contains(leg.To)) return "stale-or-outside-frame";
            bool own = poster.PosterId == source.PosterId;
            if (leg.To.Width != leg.From.Width || leg.To.Height > poster.ExpandedHeight) return "size-intent-changed";
            if (motion == ExpansionDragMotion.Move)
            {
                if (!own || poster.IsLocked || leg.To.Height != leg.From.Height) return "move-cannot-resize-or-push";
            }
            else if (own)
            {
                if (leg.To.X != leg.From.X || leg.To.Height <= leg.From.Height ||
                    leg.To.Y > leg.From.Y || leg.To.Bottom < leg.From.Bottom ||
                    (leg.To.Y != leg.From.Y && leg.To.Bottom != leg.From.Bottom)) return "growth-not-monotone-anchored";
            }
            else if (poster.IsLocked || leg.To.X != leg.From.X || leg.To.Height != leg.From.Height)
                return "invalid-neighbor-growth";
            positions[poster.PosterId] = leg.To;
        }
        if (motion == ExpansionDragMotion.Grow && !frame.Changes.Any(c => c.PosterId == source.PosterId))
            return "growth-without-source";
        var visible = state.Presented.Posters.Where(p => p.IsVisible).ToArray();
        for (int a = 0; a < visible.Length; a++)
        for (int b = a + 1; b < visible.Length; b++)
            if (SpatialPathPolicy.CrossesMoving(visible[a].Bounds, positions[visible[a].PosterId],
                visible[b].Bounds, positions[visible[b].PosterId])) return "swept-collision";
        return null;
    }

    public static ExpansionDragState AcceptPresented(ExpansionDragState state, ExpansionDragMotion motion, SpatialBatchFrame frame)
    {
        if (CheckFrame(state, motion, frame) is { } error) throw new ArgumentException(error);
        var next = frame.Changes.ToDictionary(c => c.PosterId, c => c.To, StringComparer.Ordinal);
        return state with
        {
            Presented = new(state.Presented.Environment, state.Presented.Posters.Select(p =>
                next.TryGetValue(p.PosterId, out var bounds) ? p with { Bounds = bounds } : p)),
            UserAdjusted = state.UserAdjusted || motion == ExpansionDragMotion.Move && frame.Changes.Any(c => c.From != c.To)
        };
    }

    private static SpatialPoster Source(ExpansionDragState state) =>
        state.Presented.Posters.Single(p => p.PosterId == state.Identity.Session.PosterId);
    private static bool Terminal(ExpansionDragPhase phase) => phase is ExpansionDragPhase.Completed or ExpansionDragPhase.Cancelled;
}
