namespace DesktopUp.Application;

// Accepted PRESENTATION phase, not committed IsCollapsed. Constrained expanded endpoints
// are Expanded too. The owning runtime snapshots this on pointer down.
public enum TitlePresentedPosture { Compact, Expanding, Expanded, Collapsing }
public enum TitleInteractionPhase { Pending, CompactDrag, ExpandedDrag, BlockedDrag, Released, Cancelled, Interrupted }
public enum TitlePointerEventKind { Move, Release, LegacyInvoke, Invalidate, Interrupt }
public enum TitleInteractionAction
{
    None, PauseGroup, BeginCompactDrag, BeginExpandedDrag, MoveDrag,
    FastExpand, FinishCompactDrag, FinishExpandedDrag, ResumeGroup, CancelGroup
}
public enum TitleInvalidation
{
    CaptureLost, Hidden, Removed, Locked, PolicyDisabled, EnvironmentChanged,
    Conflict, PreparationFailed, Disposed
}

public sealed record TitleInteractionToken(string OperationId, PosterInputSessionKey Session,
    long EnvironmentEpoch, Guid CaptureId);
public readonly record struct TitlePointerDelta(long X, long Y);
public sealed record TitlePress(string OperationId, long Sequence, long EnvironmentEpoch,
    PosterInputCapture Capture, PointerScreenPoint Position, TitlePresentedPosture Posture,
    AutoPosturePointer PointerEvidence, bool AutomaticEnabled = true, bool IsLocked = false,
    bool IsEditing = false, bool IsReady = true);
public sealed record TitlePointerInput(TitleInteractionToken Token, long Sequence, TitlePointerEventKind Kind,
    PointerScreenPoint Position, bool CanInvoke = false, AutoPosturePointer? PointerEvidence = null,
    TitleInvalidation Reason = TitleInvalidation.CaptureLost);

// Adapt this into the EXISTING AutoPostureObservation.ManualCollapseExitVersion.
// This is session-local evidence, not a second hover state machine or persisted setting.
public sealed record TitleAutoRearmSuppression(PosterInputSessionKey Session, long EnvironmentEpoch, long ExitVersion)
{
    public long? ForObservation(PosterInputSessionKey session, long environmentEpoch, AutoPosturePointer pointer) =>
        session == Session && environmentEpoch == EnvironmentEpoch && pointer.ExitVersion <= ExitVersion
            ? ExitVersion : null;
}

public sealed record TitleInteractionState
{
    internal TitleInteractionState(TitlePress press)
    {
        Press = press;
        Token = new(press.OperationId, press.Capture.Press.Session, press.EnvironmentEpoch, press.Capture.Id);
        Sequence = press.Sequence;
        LastPosition = press.Position;
    }

    public TitlePress Press { get; }
    public TitleInteractionToken Token { get; }
    public long Sequence { get; internal init; }
    public TitleInteractionPhase Phase { get; internal init; } = TitleInteractionPhase.Pending;
    public PointerScreenPoint LastPosition { get; internal init; }
    public TitleAutoRearmSuppression? Rearm { get; internal init; }
    public bool HasPointerCapture => Phase is TitleInteractionPhase.Pending or TitleInteractionPhase.CompactDrag
        or TitleInteractionPhase.ExpandedDrag or TitleInteractionPhase.BlockedDrag;
    public TitlePointerDelta Delta => new((long)LastPosition.X - Press.Position.X, (long)LastPosition.Y - Press.Position.Y);
    public TitlePointerDelta GrabOffset => new((long)Press.Position.X - Press.Capture.Press.Frame.X,
        (long)Press.Position.Y - Press.Capture.Press.Frame.Y);
}

// Effects are intentions only. Released ends pointer arbitration, NOT the spatial
// transaction: TI-02/03 must keep the single group owner until presentation/commit/restore.
public sealed record TitleInteractionStep(TitleInteractionState? State, TitleInteractionAction Action = TitleInteractionAction.None,
    bool Consumed = true, bool Ignored = false, string? Rejection = null);

public static class TitleInteractionModel
{
    public const int DragThresholdPixels = 5;

    public static TitleInteractionStep Begin(TitleInteractionState? previous, TitlePress press)
    {
        ArgumentNullException.ThrowIfNull(press);
        ArgumentNullException.ThrowIfNull(press.Capture);
        ArgumentNullException.ThrowIfNull(press.Capture.Press);
        var hit = press.Capture.Press;
        ValidatePointer(press.PointerEvidence);
        if (string.IsNullOrWhiteSpace(press.OperationId) || press.OperationId.Length > 128 ||
            press.Sequence < 1 || press.EnvironmentEpoch < 1 || press.Capture.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(hit.Session.PosterId) || hit.Session.Epoch < 1 ||
            hit.LayoutRevision < 1 || !hit.Frame.IsUsable || !hit.Region.Bounds.IsUsable ||
            !Enum.IsDefined(press.Posture) || !Enum.IsDefined(hit.Region.Kind))
            throw new ArgumentException("Invalid title press identity or presented frame.");
        if (!press.AutomaticEnabled || press.IsEditing || !press.IsReady ||
            hit.Region.Kind != PosterInputTargetKind.TitleBar)
            return new(previous, Consumed: false, Ignored: true, Rejection: "outside-title-contract");
        if (previous is not null)
        {
            bool sameOwner = hit.Session == previous.Token.Session && press.EnvironmentEpoch == previous.Token.EnvironmentEpoch;
            bool duplicate = sameOwner && (press.Capture.Id == previous.Token.CaptureId ||
                press.OperationId == previous.Token.OperationId);
            if (previous.HasPointerCapture || duplicate || hit.Session.PosterId != previous.Token.Session.PosterId ||
                hit.Session.Epoch < previous.Token.Session.Epoch || press.EnvironmentEpoch < previous.Token.EnvironmentEpoch ||
                sameOwner && press.Sequence <= previous.Sequence)
                return new(previous, Consumed: duplicate, Ignored: true, Rejection: "owned-or-stale-press");
        }
        var region = hit.Region.Bounds;
        if (region.X < 0 || region.Y < 0 || (long)region.X + region.Width > hit.Frame.Width ||
            (long)region.Y + region.Height > hit.Frame.Height ||
            !InsidePressRegion(hit, press.Position))
            throw new ArgumentException("Press is outside the accepted title region.");
        return new(new(press), TitleInteractionAction.PauseGroup);
    }

    public static TitleInteractionStep Advance(TitleInteractionState state, TitlePointerInput input)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);
        // Foreign input belongs elsewhere; owned duplicates must stay consumed (no Item fall-through).
        if (input.Token != state.Token) return new(state, Consumed: false, Ignored: true);
        if (input.Sequence <= state.Sequence || !state.HasPointerCapture) return new(state, Ignored: true);
        if (!Enum.IsDefined(input.Kind) || !Enum.IsDefined(input.Reason))
            throw new ArgumentException("Unknown title input.");
        var next = state with { Sequence = input.Sequence };
        if (input.Kind == TitlePointerEventKind.Invalidate)
            return new(next with { Phase = TitleInteractionPhase.Cancelled }, TitleInteractionAction.CancelGroup);
        if (input.Kind == TitlePointerEventKind.Interrupt)
        {
            // No synthetic ButtonUp or last cursor target. The host must separately revalidate
            // the still-live environment/read set and the last whole presented group.
            if (input.Reason != TitleInvalidation.CaptureLost ||
                state.Phase is not (TitleInteractionPhase.CompactDrag or TitleInteractionPhase.ExpandedDrag))
                return new(next with { Phase = TitleInteractionPhase.Cancelled }, TitleInteractionAction.CancelGroup);
            if (state.Phase == TitleInteractionPhase.ExpandedDrag)
                return new(next with { Phase = TitleInteractionPhase.Interrupted }, TitleInteractionAction.FinishExpandedDrag);
            var evidence = input.PointerEvidence;
            if (evidence is null || !ValidPointer(evidence) ||
                evidence.EnterVersion < state.Press.PointerEvidence.EnterVersion ||
                evidence.ExitVersion < state.Press.PointerEvidence.ExitVersion)
                return new(next with { Phase = TitleInteractionPhase.Cancelled }, TitleInteractionAction.CancelGroup,
                    Rejection: "interrupt-pointer-evidence-unavailable");
            return new(next with { Phase = TitleInteractionPhase.Interrupted,
                Rearm = new(state.Token.Session, state.Token.EnvironmentEpoch, evidence.ExitVersion) },
                TitleInteractionAction.FinishCompactDrag);
        }
        // Native and routed invocation notifications do not each authorize another click.
        if (input.Kind == TitlePointerEventKind.LegacyInvoke) return new(next, Ignored: true);

        next = next with { LastPosition = input.Position };
        if (input.Kind == TitlePointerEventKind.Move)
        {
            if (state.Phase == TitleInteractionPhase.BlockedDrag) return new(next);
            if (state.Phase is TitleInteractionPhase.CompactDrag or TitleInteractionPhase.ExpandedDrag)
                return new(next, input.Position == state.LastPosition ? TitleInteractionAction.None : TitleInteractionAction.MoveDrag);
            if (!ReachedThreshold(state.Press.Position, input.Position)) return new(next);
            if (state.Press.IsLocked) return new(next with { Phase = TitleInteractionPhase.BlockedDrag });
            bool expanded = state.Press.Posture == TitlePresentedPosture.Expanded;
            return new(next with { Phase = expanded ? TitleInteractionPhase.ExpandedDrag : TitleInteractionPhase.CompactDrag },
                expanded ? TitleInteractionAction.BeginExpandedDrag : TitleInteractionAction.BeginCompactDrag);
        }

        // A release can carry the last unsampled position. Do not synthesize a click after
        // crossing the threshold, nor start a new drag AFTER the physical button is up.
        if (state.Phase == TitleInteractionPhase.Pending && ReachedThreshold(state.Press.Position, input.Position))
            return new(next with { Phase = TitleInteractionPhase.Cancelled }, TitleInteractionAction.CancelGroup);
        if (state.Phase == TitleInteractionPhase.CompactDrag)
        {
            var pointer = input.PointerEvidence;
            if (pointer is null || !ValidPointer(pointer) ||
                pointer.EnterVersion < state.Press.PointerEvidence.EnterVersion ||
                pointer.ExitVersion < state.Press.PointerEvidence.ExitVersion)
                return new(next with { Phase = TitleInteractionPhase.Cancelled }, TitleInteractionAction.CancelGroup,
                    Rejection: "release-pointer-evidence-unavailable");
            return new(next with { Phase = TitleInteractionPhase.Released,
                Rearm = new(state.Token.Session, state.Token.EnvironmentEpoch, pointer.ExitVersion) },
                TitleInteractionAction.FinishCompactDrag);
        }
        if (state.Phase == TitleInteractionPhase.ExpandedDrag)
            return new(next with { Phase = TitleInteractionPhase.Released }, TitleInteractionAction.FinishExpandedDrag);
        if (state.Phase == TitleInteractionPhase.BlockedDrag)
            return new(next with { Phase = TitleInteractionPhase.Released }, TitleInteractionAction.ResumeGroup);
        if (!input.CanInvoke || !InsidePressRegion(state.Press.Capture.Press, input.Position))
            return new(next with { Phase = TitleInteractionPhase.Cancelled }, TitleInteractionAction.CancelGroup);
        return new(next with { Phase = TitleInteractionPhase.Released },
            state.Press.Posture == TitlePresentedPosture.Expanded ? TitleInteractionAction.ResumeGroup : TitleInteractionAction.FastExpand);
    }

    private static bool ReachedThreshold(PointerScreenPoint from, PointerScreenPoint to)
    {
        long x = (long)to.X - from.X, y = (long)to.Y - from.Y;
        // Avoid overflow for malformed/extreme screen deltas; ordinary behavior is the
        // same 5 physical-pixel Euclidean threshold as the existing input island.
        return Math.Abs(x) >= DragThresholdPixels || Math.Abs(y) >= DragThresholdPixels ||
            x * x + y * y >= DragThresholdPixels * DragThresholdPixels;
    }

    private static bool InsidePressRegion(PosterInputHit hit, PointerScreenPoint point)
    {
        long x = (long)point.X - hit.Frame.X, y = (long)point.Y - hit.Frame.Y;
        var r = hit.Region.Bounds;
        return x >= r.X && y >= r.Y && x < (long)r.X + r.Width && y < (long)r.Y + r.Height;
    }

    private static bool ValidPointer(AutoPosturePointer pointer) => Enum.IsDefined(pointer.Presence) &&
        pointer.EnterVersion >= 0 && pointer.ExitVersion >= 0;
    private static void ValidatePointer(AutoPosturePointer pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        if (!ValidPointer(pointer)) throw new ArgumentException("Invalid genuine pointer evidence.");
    }
}
