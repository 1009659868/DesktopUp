using DesktopUp.Domain;

namespace DesktopUp.Application;

// Opaque proposal: only its originating session can accept it, once, against the same base.
public sealed class TitleSpatialProposal
{
    internal TitleSpatialProposal(TitleSpatialState basis, TitleSpatialState next, TitleSpatialSample sample,
        IReadOnlyList<SpatialBatchFrame> frames) { Basis = basis; Next = next; Sample = sample; Frames = frames; }
    internal TitleSpatialState Basis { get; }
    internal TitleSpatialState Next { get; }
    internal TitleSpatialSample Sample { get; }
    public IReadOnlyList<SpatialBatchFrame> Frames { get; }
    // Frames remain the swept path proof. A title click presents only the prepared whole-group
    // endpoint; it must NOT replay the remaining animation merely because the proof contains it.
    public bool ImmediateGroup => Basis.Intent.Phase == TitleInteractionPhase.Pending &&
        Next.Intent.Phase == TitleInteractionPhase.Released &&
        Basis.Intent.Press.Posture != TitlePresentedPosture.Expanded;
    public SpatialSnapshot Endpoint => Next.Scene;
    public bool Complete => Next.Complete;
    public bool Cancelled => Next.Cancelled;
}

public sealed class TitleSpatialSession
{
    private readonly TitleSpatialStart _start;
    private readonly PosterContentMetrics _metrics;
    private readonly SpatialContinuityState? _initialContinuity;
    private readonly List<TitleSpatialSample> _samples = [];
    private TitleSpatialState _state;
    private TitleSpatialProposal? _pending;
    private bool _cancelled;
    private TitleProgressCheckpoint? _checkpoint;
    public long SealedSegments => _checkpoint?.Segment ?? 0;
    public bool NeedsCheckpoint => !Complete && !Cancelled && _pending is null && _samples.Count >= Math.Max(1, MotionBudget / 2);
    public const int MaximumSamples = 256;
    public int SampleCount => _samples.Count;
    public SpatialSnapshot Presented => _state.Scene;
    public SpatialContinuityState Continuity => _state.Continuity;
    public TitleInteractionState Intent => _state.Intent;
    public bool Complete => _state.Complete && !_cancelled;
    public bool Cancelled => _cancelled || _state.Cancelled;
    public bool HasUserMoved => _state.UserMoved;
    public int MotionBudget { get; }
    public TitleSpatialStart Start => _start;

    // The dispatcher must call this at prepare, accept and immediately before database commit.
    // Shell/display/capture epochs are runtime evidence; SQLite cannot discover HWND lifetime.
    public bool ValidateCurrent(SpatialEnvironment environment, TitleInteractionToken token)
    {
        if (!Cancelled && environment == _start.Initial.Environment && token == _state.Intent.Token) return true;
        Cancel();
        return false;
    }

    public TitleSpatialSession(TitleSpatialStart start, PosterContentMetrics metrics, SpatialContinuityState? continuity = null)
    {
        _start = start; _metrics = metrics;
        _initialContinuity = continuity is null ? null : continuity with { Nodes = Array.AsReadOnly(continuity.Nodes.ToArray()) };
        _state = TitleSpatialKernel.Begin(start, metrics, _initialContinuity);
        long n = start.Initial.Posters.Count;
        MotionBudget = (int)Math.Min(MaximumSamples, Math.Max(1, 4_000_000 / (n * n * n * 64)));
    }

    public TitleSpatialProposal? Propose(TitleSpatialSample sample)
    {
        _pending = null; // superseded proposals are never accepted as a hidden movement path
        if (Complete || Cancelled) return null;
        bool terminal = sample.Pointer?.Kind is TitlePointerEventKind.Release or TitlePointerEventKind.Invalidate or TitlePointerEventKind.Interrupt;
        if (_samples.Count >= MotionBudget && !terminal) return null; // reserve release/cancel, never permanent busy
        var result = TitleSpatialKernel.Advance(_start, _metrics, _state, sample, _initialContinuity);
        if (ReferenceEquals(result.State, _state)) return null;
        _pending = new(_state, result.State, sample, result.Frames);
        return _pending;
    }

    // Attest the prepared WHOLE group (ImmediateGroup endpoint, otherwise all coordinated frames).
    // false, including partial application, invalidates the proposal; physical recovery is TI-03.
    public bool Accept(TitleSpatialProposal proposal, bool presentationSucceeded)
    {
        if (Cancelled || !ReferenceEquals(_pending, proposal) || !ReferenceEquals(_state, proposal.Basis)) return false;
        _pending = null;
        if (!presentationSucceeded) { _cancelled = true; return false; }
        bool semantic = proposal.Frames.Count != 0 || proposal.Next.Intent.Phase != _state.Intent.Phase ||
            proposal.Next.Complete || proposal.Next.Cancelled;
        _state = proposal.Next;
        if (semantic) _samples.Add(proposal.Sample);
        return true;
    }
    // The driver calls this only after successful WHOLE native group frames and fresh runtime
    // checks. Replaying the sample reconstructs both the prefix geometry and causal witness.
    public bool AcceptPresentedPrefix(TitleSpatialProposal proposal, int count)
    {
        if (Cancelled || !ReferenceEquals(_pending, proposal) || !ReferenceEquals(_state, proposal.Basis) ||
            proposal.Complete || proposal.Cancelled || count <= 0 || count >= proposal.Frames.Count) return false;
        var sample = proposal.Sample with { PresentedFrames = count };
        var result = TitleSpatialKernel.Advance(_start, _metrics, _state, sample, _initialContinuity);
        var prefix = new TitleSpatialProposal(_state, result.State, sample, result.Frames);
        _pending = prefix;
        return Accept(prefix, true);
    }
    internal bool DiscardUnpresentedProposal(TitleSpatialProposal proposal)
    {
        if (Cancelled || !ReferenceEquals(_pending, proposal) || !ReferenceEquals(_state, proposal.Basis)) return false;
        _pending = null;
        return true;
    }
    public void Cancel() { _cancelled = true; _pending = null; }
    public TitleSpatialTrace Freeze() => Complete && !Cancelled && _pending is null
        ? ProgressTrace()
        : throw new InvalidOperationException("Only a fully accepted terminal presentation can be committed.");

    internal TitleSpatialTrace ProgressTrace() => _pending is null && !Cancelled
        ? new(_start, Array.AsReadOnly(_samples.ToArray()), _state.Scene, _state.Continuity) { Checkpoint = _checkpoint }
        : throw new InvalidOperationException("Only whole accepted proposals may be sealed.");

    internal TitleSpatialState AcceptedState => _state;
    internal bool Seal(TitleProgressCheckpoint checkpoint)
    {
        if (_pending is not null || Complete || Cancelled || checkpoint.Start != _start ||
            !SameState(_state, checkpoint.State)) return false;
        _checkpoint = checkpoint; _samples.Clear();
        return true;
    }
    internal static bool SameContinuity(SpatialContinuityState? a, SpatialContinuityState? b) =>
        a is null ? b is null : b is not null && a.Environment == b.Environment &&
        a.SchemaVersion == b.SchemaVersion && a.Nodes.SequenceEqual(b.Nodes);
    internal static bool SameState(TitleSpatialState a, TitleSpatialState b) =>
        a.Intent == b.Intent && a.UserMoved == b.UserMoved && a.Complete == b.Complete && a.Cancelled == b.Cancelled &&
        a.Scene.Environment == b.Scene.Environment && a.Scene.Posters.SequenceEqual(b.Scene.Posters) &&
        SameContinuity(a.Continuity, b.Continuity);

    public static TitleSpatialSession Replay(TitleSpatialTrace trace, PosterContentMetrics metrics,
        SpatialContinuityState? continuity = null) => ReplayBounded(trace, metrics, continuity, null, true);

    internal static TitleSpatialSession ReplayBounded(TitleSpatialTrace trace, PosterContentMetrics metrics,
        SpatialContinuityState? continuity, TitleProgressCheckpoint? checkpoint, bool requireComplete)
    {
        if (!ReferenceEquals(trace.Checkpoint, checkpoint) || trace.Samples.Count > MaximumSamples + 1)
            throw new ArgumentException("Untrusted prefix or title trace budget exceeded.");
        var session = new TitleSpatialSession(trace.Start, metrics, continuity);
        if (checkpoint is not null)
        {
            if (checkpoint.Start != trace.Start || checkpoint.Metrics != metrics ||
                !SameContinuity(checkpoint.InitialContinuity, continuity))
                throw new ArgumentException("Checkpoint context changed.");
            session._state = checkpoint.State;
            session._checkpoint = checkpoint;
        }
        foreach (var sample in trace.Samples)
        {
            var proposal = session.Propose(sample);
            if (proposal is null || !session.Accept(proposal, true)) throw new ArgumentException("Invalid semantic replay.");
        }
        if (session.Cancelled || requireComplete && !session.Complete)
            throw new ArgumentException("Title trace is not a completed/accepted operation.");
        if (trace.Presented.Environment != session.Presented.Environment ||
            !trace.Presented.Posters.SequenceEqual(session.Presented.Posters) ||
            !SameContinuity(trace.Continuity, session.Continuity))
            throw new ArgumentException("Replay differs from accepted presentation and causal witness.");
        return session;
    }
}
