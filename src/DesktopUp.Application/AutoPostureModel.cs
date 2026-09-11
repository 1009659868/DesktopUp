using System.Collections.ObjectModel;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum AutoPosturePresence { Unknown, Outside, Inside, ExpansionZone }
public enum AutoPosturePhase { Disabled, Unavailable, Protected, AwaitingEntry, ManualSuppressed, CountingDown, Eligible, InFlight, Attempted }
public enum AutoPostureOutcome { Committed, InsufficientSpace, Conflict, Cancelled, HostUnavailable }

[Flags]
public enum AutoPostureProtection
{
    None = 0, Editing = 1, Menu = 2, Confirmation = 4, PointerButton = 8,
    DragDrop = 16, Gesture = 32, Recovery = 64,
    // A native/routed press capture is not proof of a semantic poster drag.
    PointerCapture = 128
}

public sealed record AutoPosturePolicy(bool Enabled = false, int LeaveDelaySeconds = PosterAutoPosturePolicy.DefaultDelaySeconds)
{
    public static AutoPosturePolicy From(PosterDefinition poster) => new(poster.AutoCollapseEnabled, poster.AutoCollapseDelaySeconds);

    public void Validate()
    {
        if (PosterAutoPosturePolicy.ValidateDelay(LeaveDelaySeconds) is not null)
            throw new ArgumentOutOfRangeException(nameof(LeaveDelaySeconds));
    }
}

// Versions count genuine user boundary evidence, never bounds changes under a stationary cursor.
// Separate exit evidence preserves a leave/re-enter pair even when only the final sample is observed.
public sealed record AutoPosturePointer(AutoPosturePresence Presence, long EnterVersion = 0, long ExitVersion = 0);

public sealed record AutoPostureObservation(
    PosterInputSessionKey Session, long EntityRevision, long EnvironmentEpoch,
    AutoPosturePolicy Policy, bool IsCollapsed, bool IsReady, bool IsVisible,
    AutoPosturePointer Pointer, long ManualIntentVersion = 0, long SpaceVersion = 0,
    AutoPostureProtection Protection = AutoPostureProtection.None, long? ManualCollapseExitVersion = null);

public sealed record AutoPostureRequest(
    long Id, PosterInputSessionKey Session, long ExpectedEntityRevision, long EnvironmentEpoch,
    AutoPosturePolicy Policy, long ManualIntentVersion, bool Collapse);

public sealed record AutoPostureCompletion(AutoPostureRequest Request, AutoPostureOutcome Outcome);
public sealed record AutoPostureFrame(long Sequence, long NowMs, IReadOnlyList<AutoPostureObservation> Posters,
    bool SpatialBusy = false, AutoPostureProtection SharedProtection = AutoPostureProtection.None,
    AutoPostureCompletion? Completion = null);

public sealed record AutoPostureNode(
    AutoPostureObservation Last, AutoPosturePhase Phase,
    long? DeadlineMs = null, long? EligibleSinceMs = null, bool ExpansionArmed = false,
    bool AttemptConsumed = false, long? SuppressedAtExitVersion = null);

public sealed class AutoPostureState
{
    internal AutoPostureState(long sequence, long nowMs, long nextRequestId,
        Dictionary<string, AutoPostureNode> posters, AutoPostureRequest? outstanding, bool cancellationIssued)
    {
        Sequence = sequence;
        NowMs = nowMs;
        NextRequestId = nextRequestId;
        Posters = new ReadOnlyDictionary<string, AutoPostureNode>(posters);
        Outstanding = outstanding;
        CancellationIssued = cancellationIssued;
    }

    public static AutoPostureState Empty { get; } = new(0, 0, 1, new(StringComparer.Ordinal), null, false);
    public long Sequence { get; }
    public long NowMs { get; }
    public long NextRequestId { get; }
    public IReadOnlyDictionary<string, AutoPostureNode> Posters { get; }
    public AutoPostureRequest? Outstanding { get; }
    public bool CancellationIssued { get; }
}

// Request and Cancel are effects for the scheduler, not executed commands. Cancellation never
// releases the single-flight slot until the matching completion acknowledges termination.
public sealed record AutoPostureStep(AutoPostureState State, AutoPostureRequest? Request = null,
    AutoPostureRequest? Cancel = null, bool Ignored = false);

public static class AutoPostureModel
{
    private const AutoPostureProtection AllProtection = AutoPostureProtection.Editing | AutoPostureProtection.Menu |
        AutoPostureProtection.Confirmation | AutoPostureProtection.PointerButton | AutoPostureProtection.DragDrop |
        AutoPostureProtection.Gesture | AutoPostureProtection.Recovery | AutoPostureProtection.PointerCapture;
    private const AutoPostureProtection AdmissionOnly =
        AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture;

    public static AutoPostureStep Advance(AutoPostureState state, AutoPostureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(frame);
        // Replayed/late frames cannot reintroduce a removed session or release a newer request.
        if (frame.Sequence <= state.Sequence || frame.NowMs < state.NowMs) return new(state, Ignored: true);
        if (frame.NowMs < 0 || frame.NowMs > long.MaxValue - 300_000)
            throw new ArgumentOutOfRangeException(nameof(frame.NowMs));
        ArgumentNullException.ThrowIfNull(frame.Posters);
        ValidateProtection(frame.SharedProtection);
        var observations = frame.Posters.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var observation in observations)
        {
            Validate(observation);
            if (!ids.Add(observation.Session.PosterId)) throw new ArgumentException("Duplicate poster identity.");
        }
        if (frame.Completion is { } completion)
        {
            ArgumentNullException.ThrowIfNull(completion.Request);
            if (!Enum.IsDefined(completion.Outcome)) throw new ArgumentException("Invalid completion outcome.");
        }

        // An adapter must provide a complete, authoritative catalog sample. Do not partially apply
        // a sample with a regressed per-session evidence/version counter.
        foreach (var current in observations)
            if (state.Posters.TryGetValue(current.Session.PosterId, out var previous) &&
                Regressed(previous.Last, current)) return new(state, Ignored: true);

        var outstanding = state.Outstanding;
        var finished = frame.Completion is { } done && done.Request == outstanding ? done : null;
        bool cancellationIssued = state.CancellationIssued;
        if (finished is not null)
        {
            outstanding = null;
            cancellationIssued = false;
        }

        var nodes = new Dictionary<string, AutoPostureNode>(StringComparer.Ordinal);
        foreach (var observation in observations.OrderBy(p => p.Session.PosterId, StringComparer.Ordinal))
        {
            state.Posters.TryGetValue(observation.Session.PosterId, out var previous);
            nodes.Add(observation.Session.PosterId, Observe(previous, observation, frame,
                outstanding?.Session == observation.Session, finished));
        }

        AutoPostureRequest? cancel = null;
        if (outstanding is not null && !cancellationIssued &&
            (!nodes.TryGetValue(outstanding.Session.PosterId, out var owner) ||
             Invalidated(outstanding, owner.Last, frame.SharedProtection)))
        {
            cancel = outstanding;
            cancellationIssued = true;
        }

        AutoPostureRequest? request = null;
        long nextId = state.NextRequestId;
        if (outstanding is null && !frame.SpatialBusy && frame.SharedProtection == AutoPostureProtection.None)
        {
            var candidate = nodes.Values.Where(n => n.Phase == AutoPosturePhase.Eligible)
                .OrderBy(n => n.EligibleSinceMs).ThenBy(n => n.Last.Session.PosterId, StringComparer.Ordinal).FirstOrDefault();
            if (candidate is not null)
            {
                var o = candidate.Last;
                request = new(nextId, o.Session, o.EntityRevision, o.EnvironmentEpoch,
                    o.Policy, o.ManualIntentVersion, !o.IsCollapsed);
                nextId = checked(nextId + 1);
                outstanding = request;
                nodes[o.Session.PosterId] = ClearWork(candidate) with { Phase = AutoPosturePhase.InFlight };
            }
        }

        return new(new(frame.Sequence, frame.NowMs, nextId, nodes, outstanding, cancellationIssued), request, cancel);
    }

    private static AutoPostureNode Observe(AutoPostureNode? previous, AutoPostureObservation o,
        AutoPostureFrame frame, bool inFlight, AutoPostureCompletion? finished)
    {
        bool reset = previous is null || previous.Last.Session != o.Session ||
            previous.Last.EnvironmentEpoch != o.EnvironmentEpoch || previous.Last.Policy != o.Policy;
        var n = reset ? new AutoPostureNode(o, AutoPosturePhase.AwaitingEntry) : previous! with { Last = o };
        bool manual = !reset && o.ManualIntentVersion != previous!.Last.ManualIntentVersion;
        bool entered = !reset && o.Pointer.EnterVersion > previous!.Last.Pointer.EnterVersion;
        bool exited = !reset && o.Pointer.ExitVersion > previous!.Last.Pointer.ExitVersion;
        bool spaceChanged = !reset && o.SpaceVersion > previous!.Last.SpaceVersion;
        bool postureChanged = !reset && o.IsCollapsed != previous!.Last.IsCollapsed;
        bool resumed = previous is not null && IsInactive(previous.Phase);

        if (manual)
            n = ClearWork(n) with
            {
                AttemptConsumed = false,
                // Confirmed manual priority: collapse is not undone until a genuine subsequent exit.
                SuppressedAtExitVersion = o.IsCollapsed && o.Pointer.Presence != AutoPosturePresence.Outside
                    ? o.Pointer.ExitVersion : null
            };
        if (n.SuppressedAtExitVersion is { } exit && o.Pointer.ExitVersion > exit)
            n = n with { SuppressedAtExitVersion = null };
        // A manual collapse can move its title away from an unmoving pointer. Retain the
        // genuine exit counter captured at intent time, not only the completion-time presence.
        if (o.IsCollapsed && o.ManualCollapseExitVersion is { } manualExit && o.Pointer.ExitVersion <= manualExit)
            n = n with { SuppressedAtExitVersion = manualExit };
        if (postureChanged)
            n = ClearWork(n) with { AttemptConsumed = false };
        if (finished?.Request.Session == o.Session && finished.Request.EnvironmentEpoch == o.EnvironmentEpoch &&
            finished.Request.Policy == o.Policy && finished.Request.ManualIntentVersion == o.ManualIntentVersion &&
            !reset && !manual)
        {
            n = ClearWork(n) with
            {
                AttemptConsumed = finished.Outcome != AutoPostureOutcome.Committed ||
                    o.IsCollapsed != finished.Request.Collapse
            };
        }

        if (!o.Policy.Enabled) return Inactive(n, AutoPosturePhase.Disabled);
        if (!o.IsReady || !o.IsVisible || o.Pointer.Presence == AutoPosturePresence.Unknown)
            return Inactive(n, AutoPosturePhase.Unavailable);
        var protection = o.Protection | frame.SharedProtection;
        // Raw clicks stop admission, not an already dispatched transaction. Keep semantic
        // manual gestures, menus, settings, lifecycle and unknown-input cancellation intact.
        if (inFlight) protection &= ~AdmissionOnly;
        if (protection != AutoPostureProtection.None)
            return Inactive(n, AutoPosturePhase.Protected);
        if (inFlight) return ClearWork(n) with { Phase = AutoPosturePhase.InFlight };

        // Recovery/initial synchronization is not fresh hover intent. A genuine event received
        // while blocked is not replayed after the blocker closes.
        bool newEntry = entered && !manual && !resumed && !postureChanged;
        if (o.IsCollapsed)
        {
            if (n.SuppressedAtExitVersion is not null)
                return ClearWork(n) with { Phase = AutoPosturePhase.ManualSuppressed };
            if (o.Pointer.Presence != AutoPosturePresence.ExpansionZone)
                return ClearWork(n) with
                {
                    Phase = AutoPosturePhase.AwaitingEntry,
                    AttemptConsumed = exited ? false : n.AttemptConsumed
                };
            if (newEntry || (spaceChanged && n.AttemptConsumed && !resumed))
                n = n with { ExpansionArmed = true, AttemptConsumed = false, EligibleSinceMs = frame.NowMs };
            if (!n.ExpansionArmed || n.AttemptConsumed)
                return ClearWork(n) with { Phase = n.AttemptConsumed ? AutoPosturePhase.Attempted : AutoPosturePhase.AwaitingEntry };
            return n with { Phase = AutoPosturePhase.Eligible, EligibleSinceMs = n.EligibleSinceMs ?? frame.NowMs };
        }

        if (o.Pointer.Presence != AutoPosturePresence.Outside)
            return ClearWork(n) with { Phase = AutoPosturePhase.AwaitingEntry, AttemptConsumed = false };
        // A complete re-entry/exit pair between samples also starts a fresh leave delay.
        if (exited) n = ClearWork(n);
        if (manual || exited || spaceChanged || resumed) n = n with { AttemptConsumed = false };
        if (n.AttemptConsumed) return ClearWork(n) with { Phase = AutoPosturePhase.Attempted };
        long due = n.DeadlineMs ?? checked(frame.NowMs + o.Policy.LeaveDelaySeconds * 1000L);
        return n with
        {
            Phase = frame.NowMs >= due ? AutoPosturePhase.Eligible : AutoPosturePhase.CountingDown,
            DeadlineMs = due, EligibleSinceMs = frame.NowMs >= due ? due : null, ExpansionArmed = false
        };
    }

    private static AutoPostureNode ClearWork(AutoPostureNode n) =>
        n with { DeadlineMs = null, EligibleSinceMs = null, ExpansionArmed = false };
    private static AutoPostureNode Inactive(AutoPostureNode n, AutoPosturePhase phase) =>
        ClearWork(n) with { Phase = phase, AttemptConsumed = false };
    private static bool IsInactive(AutoPosturePhase phase) =>
        phase is AutoPosturePhase.Disabled or AutoPosturePhase.Unavailable or AutoPosturePhase.Protected;
    private static bool Invalidated(AutoPostureRequest r, AutoPostureObservation o, AutoPostureProtection shared) =>
        r.Session != o.Session || r.EnvironmentEpoch != o.EnvironmentEpoch || r.Policy != o.Policy ||
        r.ManualIntentVersion != o.ManualIntentVersion || !o.IsReady || !o.IsVisible ||
        o.Pointer.Presence == AutoPosturePresence.Unknown ||
        ((o.Protection | shared) & ~AdmissionOnly) != AutoPostureProtection.None;

    private static bool Regressed(AutoPostureObservation old, AutoPostureObservation current)
    {
        if (current.Session.Epoch < old.Session.Epoch) return true;
        if (current.Session != old.Session) return false;
        return current.EntityRevision < old.EntityRevision || current.EnvironmentEpoch < old.EnvironmentEpoch ||
            current.ManualIntentVersion < old.ManualIntentVersion || current.SpaceVersion < old.SpaceVersion ||
            current.Pointer.EnterVersion < old.Pointer.EnterVersion || current.Pointer.ExitVersion < old.Pointer.ExitVersion;
    }

    private static void Validate(AutoPostureObservation o)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(o.Policy);
        ArgumentNullException.ThrowIfNull(o.Pointer);
        o.Policy.Validate();
        if (string.IsNullOrWhiteSpace(o.Session.PosterId) || o.Session.Epoch < 1 ||
            o.EntityRevision < 0 || o.EnvironmentEpoch < 1 || o.ManualIntentVersion < 0 || o.SpaceVersion < 0 ||
            o.Pointer.EnterVersion < 0 || o.Pointer.ExitVersion < 0 || o.ManualCollapseExitVersion < 0 || !Enum.IsDefined(o.Pointer.Presence))
            throw new ArgumentException("Invalid auto-posture observation.");
        ValidateProtection(o.Protection);
    }

    private static void ValidateProtection(AutoPostureProtection protection)
    {
        if ((protection & ~AllProtection) != 0) throw new ArgumentException("Invalid interaction protection.");
    }
}
