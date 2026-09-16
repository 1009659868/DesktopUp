using DesktopUp.Domain;

namespace DesktopUp.Application;

// Dispatcher-confined adapter used by production and fake-presenter tests. It preserves the
// first threshold crossing once, then newest motion. A verified group boundary may retire an
// obsolete tail; an interruption keeps that boundary without synthesizing pointer release.
public sealed class TitlePresentationDriver(TitleSpatialSession session)
{
    private TitlePointerInput? _firstMove, _latestMove, _release, _interrupt;
    private TitleSpatialProposal? _proposal;
    private IReadOnlyList<SpatialBatchFrame> _frames = [];
    private int _frameIndex;
    private long _sequence = session.Intent.Sequence;
    private readonly Dictionary<string, SpatialRect> _physical = session.Presented.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
    public TitleSpatialSession Session { get; } = session;
    public IReadOnlyDictionary<string, SpatialRect> Physical => _physical;
    public bool Released { get; private set; }
    public bool Interrupted { get; private set; }
    public bool InputEnded => Released || Interrupted;
    public bool HasPendingPresentation => _proposal is not null;
    public int AcceptedFrames { get; private set; }
    public bool WasFastExpand { get; private set; }
    public string? FailureReason { get; private set; }

    public bool Queue(TitlePointerInput input)
    {
        if (input.Token != Session.Intent.Token || input.Sequence <= _sequence ||
            InputEnded && input.Kind != TitlePointerEventKind.Invalidate || Session.Cancelled || Session.Complete) return false;
        _sequence = input.Sequence;
        if (input.Kind == TitlePointerEventKind.Invalidate) { Cancel("invalidated-" + input.Reason); return true; }
        if (input.Kind == TitlePointerEventKind.Interrupt)
        {
            Interrupted = true; _interrupt = input; _firstMove = null; _latestMove = null;
            return true;
        }
        if (input.Kind == TitlePointerEventKind.Release) { Released = true; _release = input; return true; }
        if (input.Kind != TitlePointerEventKind.Move) return true;
        if (Session.Intent.Phase != TitleInteractionPhase.Pending) { _latestMove = input; _firstMove = null; }
        else if (_firstMove is null) _firstMove = input;
        else if (Session.Intent.Phase == TitleInteractionPhase.Pending &&
            TitleInteractionModel.Advance(Session.Intent, _firstMove).State?.Phase == TitleInteractionPhase.Pending)
            _firstMove = input;
        else _latestMove = input;
        return true;
    }

    public void Tick(Func<bool> current, Func<SpatialBatchFrame, bool> present)
    {
        if (Session.Complete || Session.Cancelled) return;
        try
        {
            if (!current()) { Cancel("generation-before-proposal"); return; }
            var newest = _latestMove ?? _firstMove;
            if (_proposal is { Complete: false, Cancelled: false } pending &&
                pending.Next.Intent.Phase is TitleInteractionPhase.CompactDrag or TitleInteractionPhase.ExpandedDrag &&
                (_interrupt is not null || newest is not null &&
                    newest.Sequence > (pending.Sample.Pointer?.Sequence ?? Session.Intent.Sequence) &&
                    (_frameIndex > 0 || Session.Intent.Phase != TitleInteractionPhase.Pending)))
            {
                bool retained = _frameIndex > 0
                    ? Session.AcceptPresentedPrefix(pending, _frameIndex)
                    : Session.DiscardUnpresentedProposal(pending);
                if (!retained || !current()) { Cancel("presented-prefix-rejected"); return; }
                _proposal = null; _frames = []; _frameIndex = 0;
                // Let the existing runtime seal this accepted boundary before another bent
                // proposal can keep the checkpoint gate perpetually busy. Input stays queued.
                if (Session.NeedsCheckpoint) return;
            }
            if (_proposal is null)
            {
                TitlePointerInput? input;
                if (_interrupt is not null) { input = _interrupt; _interrupt = null; }
                else if (Session.Intent.Phase == TitleInteractionPhase.Pending)
                { input = _firstMove; _firstMove = _latestMove; _latestMove = null; }
                else { input = _latestMove ?? _firstMove; _firstMove = null; _latestMove = null; }
                if (input is null)
                {
                    if (_release is not null) { input = _release; _release = null; }
                    else if (Session.Intent.Phase != TitleInteractionPhase.CompactDrag ||
                        Session.Presented.Posters.Single(p => p.PosterId == Session.Intent.Token.Session.PosterId).IsCollapsed) return;
                }
                _proposal = Session.Propose(new(input));
                if (_proposal is null)
                {
                    if (Session.SampleCount >= Session.MotionBudget) Cancel("motion-budget-exhausted");
                    return;
                }
                _frameIndex = 0;
                WasFastExpand |= _proposal.ImmediateGroup;
                _frames = _proposal.ImmediateGroup
                    ? new[] { new SpatialBatchFrame(_proposal.Endpoint.Posters.Select(p =>
                        new SpatialPathLeg(p.PosterId, _physical[p.PosterId], p.Bounds)).Where(p => p.From != p.To).ToArray()) }
                    : _proposal.Frames;
            }
            if (_frameIndex < _frames.Count)
            {
                var frame = _frames[_frameIndex];
                if (frame.Changes.Any(p => _physical[p.PosterId] != p.From)) { Cancel("presented-origin-mismatch"); return; }
                if (!current()) { Cancel("runtime-before-batch"); return; }
                if (frame.Changes.Count > 0 && !present(frame)) { Cancel("native-batch-rejected"); return; }
                foreach (var leg in frame.Changes) _physical[leg.PosterId] = leg.To;
                _frameIndex++;
                AcceptedFrames++;
            }
            if (_frameIndex != _frames.Count) return;
            // Recheck AFTER native batch/readback, BEFORE attesting the opaque proposal.
            if (!current() || !Session.Accept(_proposal, true)) { Cancel("generation-or-proposal-acceptance"); return; }
            _proposal = null;
        }
        catch { Cancel("presenter-exception"); throw; }
    }

    public void Cancel(string reason = "cancelled")
    {
        FailureReason ??= reason;
        Session.Cancel();
        _proposal = null; _firstMove = null; _latestMove = null; _release = null; _interrupt = null; _frames = [];
    }
}
