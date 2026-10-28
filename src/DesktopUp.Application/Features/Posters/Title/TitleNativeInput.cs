namespace DesktopUp.Application;

// Original input island/capture, not a second window or global hook.
public enum TitleNativeInputKind { Press, Move, Release, Cancel }
public enum TitleNativeEndReason { None, CaptureTransferred, CancelMode, RouteInvalidated, CursorUnavailable, CaptureUnavailable, Programmatic }
public static class TitleNativeEndPolicy
{
    public static TitleNativeEndReason Resolve(TitleNativeEndReason cause, bool routeOwned, bool cursorAvailable) =>
        !routeOwned ? TitleNativeEndReason.RouteInvalidated :
        !cursorAvailable ? TitleNativeEndReason.CursorUnavailable : cause;
    public static bool AllowsSafeStop(TitleNativeEndReason cause) => cause == TitleNativeEndReason.CaptureTransferred;
}
public sealed class TitleNativeInputEventArgs(TitleNativeInputKind kind, PosterInputCapture capture,
    PointerScreenPoint position, long sequence, bool CanInvoke = false,
    TitleNativeEndReason EndReason = TitleNativeEndReason.None) : EventArgs
{
    public TitleNativeInputKind Kind { get; } = kind;
    public PosterInputCapture Capture { get; } = capture;
    public PointerScreenPoint Position { get; } = position;
    public long Sequence { get; } = sequence;
    public bool CanInvoke { get; } = CanInvoke;
    public TitleNativeEndReason EndReason { get; } = EndReason;
    // Set on Press. Ownership stays with the original capture through release/cancel.
    public bool Handled { get; set; }
}
public sealed record TitlePointerEvidence(long EnvironmentEpoch, AutoPosturePointer Pointer);
