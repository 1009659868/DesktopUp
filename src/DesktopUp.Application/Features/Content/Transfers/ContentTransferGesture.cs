namespace DesktopUp.Application;

// A boundary arbiter for the existing content gesture, not a new layout/capture implementation.
// Feed events only after native drag-distance qualification; there is no long-press timer.
public enum ContentTransferGesturePhase { Pressed, Internal, NativeOwned, CancelPending, AwaitingResult, Completed, Cancelled }
public enum ContentTransferGestureSignal
{
    InternalMotion, ExternalMotion, LocalRelease, NativeReturnedInside, NativeDroppedOutside,
    NativeCancelled, Cancel, Invalidate
}
[Flags]
public enum ContentTransferGestureEffect
{
    None = 0, PreviewLayout = 1, ClearLayoutPreview = 2, StartNativeOnce = 4,
    CommitLayoutOnce = 8, AwaitFileResult = 16, CancelNative = 32, CompleteClick = 64
}
public sealed record ContentTransferGestureToken(Guid CaptureId, string PosterId, long Generation);
public sealed class ContentTransferGesture
{
    private long _lastSequence;
    private bool _qualified;
    public ContentTransferGesture(ContentTransferGestureToken token, IReadOnlyList<Guid> selectedCards)
    {
        if (token.CaptureId == Guid.Empty || token.Generation < 1 || string.IsNullOrWhiteSpace(token.PosterId) ||
            selectedCards.Count is < 1 or > ContentTransferPlanning.MaximumBatch ||
            selectedCards.Any(id => id == Guid.Empty) || selectedCards.Distinct().Count() != selectedCards.Count)
            throw new ArgumentException("Capture identity and a frozen selection are required.");
        Token = token; SelectedCards = Array.AsReadOnly(selectedCards.ToArray());
    }
    public ContentTransferGestureToken Token { get; }
    public IReadOnlyList<Guid> SelectedCards { get; }
    public ContentTransferGesturePhase Phase { get; private set; } = ContentTransferGesturePhase.Pressed;
    public bool SuppressOpen => _qualified || Phase is ContentTransferGesturePhase.Cancelled or ContentTransferGesturePhase.CancelPending;
    public ContentTransferGestureEffect Apply(ContentTransferGestureToken token, long sequence,
        ContentTransferGestureSignal signal, bool validInternalPreview = false)
    {
        if (token != Token || sequence <= _lastSequence || !Enum.IsDefined(signal) ||
            Phase is ContentTransferGesturePhase.Completed or ContentTransferGesturePhase.Cancelled or ContentTransferGesturePhase.AwaitingResult)
            return ContentTransferGestureEffect.None;
        _lastSequence = sequence;
        if (signal is ContentTransferGestureSignal.Cancel or ContentTransferGestureSignal.Invalidate)
        {
            if (Phase == ContentTransferGesturePhase.CancelPending) return ContentTransferGestureEffect.None;
            bool native = Phase == ContentTransferGesturePhase.NativeOwned;
            Phase = native ? ContentTransferGesturePhase.CancelPending : ContentTransferGesturePhase.Cancelled;
            return ContentTransferGestureEffect.ClearLayoutPreview | (native ? ContentTransferGestureEffect.CancelNative : ContentTransferGestureEffect.None);
        }
        if (Phase is ContentTransferGesturePhase.NativeOwned or ContentTransferGesturePhase.CancelPending)
        {
            bool cancelling = Phase == ContentTransferGesturePhase.CancelPending;
            switch (signal)
            {
                case ContentTransferGestureSignal.NativeDroppedOutside:
                    // Even after Esc/hide/exit, late target acceptance must go to the durable
                    // result reconciler; cancellation cannot pretend an accepted move never happened.
                    Phase = ContentTransferGesturePhase.AwaitingResult;
                    return ContentTransferGestureEffect.AwaitFileResult;
                case ContentTransferGestureSignal.NativeReturnedInside:
                    Phase = cancelling || !validInternalPreview ? ContentTransferGesturePhase.Cancelled : ContentTransferGesturePhase.Completed;
                    return Phase == ContentTransferGesturePhase.Completed ? ContentTransferGestureEffect.CommitLayoutOnce : ContentTransferGestureEffect.ClearLayoutPreview;
                case ContentTransferGestureSignal.NativeCancelled:
                    Phase = ContentTransferGesturePhase.Cancelled;
                    return ContentTransferGestureEffect.ClearLayoutPreview;
                default:
                    // Local capture-up/loss is expected during handoff, not a second owner.
                    return ContentTransferGestureEffect.None;
            }
        }
        switch (signal)
        {
            case ContentTransferGestureSignal.InternalMotion:
                _qualified = true; Phase = ContentTransferGesturePhase.Internal;
                return ContentTransferGestureEffect.PreviewLayout;
            case ContentTransferGestureSignal.ExternalMotion:
                _qualified = true; Phase = ContentTransferGesturePhase.NativeOwned;
                return ContentTransferGestureEffect.ClearLayoutPreview | ContentTransferGestureEffect.StartNativeOnce;
            case ContentTransferGestureSignal.LocalRelease:
                bool click = Phase == ContentTransferGesturePhase.Pressed;
                Phase = ContentTransferGesturePhase.Completed;
                return click ? ContentTransferGestureEffect.CompleteClick : validInternalPreview
                    ? ContentTransferGestureEffect.CommitLayoutOnce : ContentTransferGestureEffect.ClearLayoutPreview;
            default: return ContentTransferGestureEffect.None;
        }
    }
}

