using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ContentOpenPreparationStatus { Ready, InvalidIntent, StalePresentation, NotFound, Denied }
public enum ContentOpenPhase { Prepared, Queued, Running, Completed }
public enum ContentOpenOutcome { None, Succeeded, Handled, Rejected, Busy, Cancelled, Unavailable, Failed, TimedOut }
public enum ContentOpenEvidence { None, ShellAccepted, ProcessCreated, ProviderConfirmed }
public sealed record ContentOpenRequest(Guid ActivationId, ContentRelationWitness ReadSet,
    ContentSourceIdentity Source, ContentOpenTarget Target, ContentOpenOrigin Origin);
public sealed record ContentOpenPreparation(ContentOpenPreparationStatus Status, ContentOpenRequest? Request = null,
    ContentCapabilityDenial Denial = ContentCapabilityDenial.None);
public sealed record ContentOpenCompletion(Guid ActivationId, string ProviderId, ContentOpenOutcome Outcome,
    ContentOpenEvidence Evidence, DateTimeOffset ObservedAt);
public sealed record ContentOpenFact(Guid ActivationId, Guid ContentItemId, string PosterId, Guid PosterItemId,
    ContentSourceIdentity Source, long ItemVersion, ContentOpenEvidence Evidence, DateTimeOffset ObservedAt);
public sealed record ContentOpenState(ContentOpenRequest Request, ContentOpenPhase Phase = ContentOpenPhase.Prepared,
    ContentOpenOutcome Outcome = ContentOpenOutcome.None, bool CancellationRequested = false, bool WaitExpired = false);
public sealed record ContentOpenTransition(ContentOpenState State, bool Accepted, ContentOpenFact? NewFact = null,
    string? Reason = null);

public enum ContentOpenRequestAdmission { Ready, Duplicate, Conflict, Busy }
public sealed record ContentOpenRequestDecision(ContentOpenRequestAdmission Status, ContentOpenState? NewState = null);
public static class ContentOpenRequestPolicy
{
    // Pure unique-request/admission decision. The future coordinator must read and insert
    // atomically; a completed token stays a tombstone and is never used for a second dispatch.
    public static ContentOpenRequestDecision Prepare(IReadOnlyCollection<ContentOpenState> existing,
        ContentOpenRequest request, int maximumActive)
    {
        if (maximumActive < 1) throw new ArgumentOutOfRangeException(nameof(maximumActive));
        var candidate = ContentOpenContract.Begin(request);
        var sameId = existing.Where(x => x.Request.ActivationId == request.ActivationId).ToArray();
        if (sameId.Length > 0)
            return new(sameId.Length == 1 && sameId[0].Request == request
                ? ContentOpenRequestAdmission.Duplicate : ContentOpenRequestAdmission.Conflict);
        var active = existing.Where(x => x.Phase != ContentOpenPhase.Completed).ToArray();
        if (active.Length >= maximumActive || active.Any(x =>
            x.Request.Source.ProviderId == request.Source.ProviderId && x.Request.Target == request.Target))
            return new(ContentOpenRequestAdmission.Busy);
        return new(ContentOpenRequestAdmission.Ready, candidate);
    }
}

// Future trusted host boundary. No implementation/registration is added to the DEMO production path.
public interface IContentOpenDispatcher
{
    ValueTask<ContentOpenCompletion> DispatchAsync(ContentOpenRequest request, CancellationToken cancellationToken);
}

public static class ContentOpenContract
{
    public static ContentOpenPreparation Prepare(ContentCatalogSnapshot catalog, ContentOpenIntent intent,
        ContentActivationTarget currentPresentation, ContentTransferRole transfer = ContentTransferRole.None)
    {
        if (intent is null || intent.ActivationId == Guid.Empty || !Enum.IsDefined(intent.Origin) ||
            intent.Target is null || string.IsNullOrWhiteSpace(intent.Target.PosterId) ||
            string.IsNullOrWhiteSpace(intent.Target.ItemId) || string.IsNullOrWhiteSpace(intent.Target.PresentationWitness) ||
            intent.Target.ItemSetVersion < 1 || intent.Target.DocumentVersion < 1)
            return new(ContentOpenPreparationStatus.InvalidIntent);
        if (intent.Target != currentPresentation) return new(ContentOpenPreparationStatus.StalePresentation);
        var resolved = ContentRelationPolicy.Resolve(catalog, intent.Target.PosterId, intent.Target.ItemId);
        if (resolved is null) return new(ContentOpenPreparationStatus.NotFound);
        var capability = ContentCapabilityPolicy.Evaluate(resolved.Item, ContentOperation.Open, transfer);
        return capability.Allowed
            ? new(ContentOpenPreparationStatus.Ready, new(intent.ActivationId, resolved.Witness,
                resolved.Item.Source, resolved.Item.Target, intent.Origin))
            : new(ContentOpenPreparationStatus.Denied, Denial: capability.Denial);
    }

    public static ContentOpenState Begin(ContentOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ActivationId == Guid.Empty || request.ReadSet is null || request.Source is null ||
            request.Target is null || string.IsNullOrWhiteSpace(request.Source.ProviderId) || !Enum.IsDefined(request.Origin))
            throw new ArgumentException("Opening requires a prepared, attributed request.");
        return new(request);
    }

    public static ContentOpenTransition Queue(ContentOpenState state) =>
        state.Phase == ContentOpenPhase.Prepared
            ? new(state with { Phase = ContentOpenPhase.Queued }, true) : Ignore(state, "Already queued/running/finished.");

    // Called immediately before dispatch from the authoritative current view. No path IO.
    public static ContentOpenTransition Start(ContentOpenState state, ContentCatalogSnapshot catalog,
        ContentTransferRole transfer = ContentTransferRole.None)
    {
        if (state.Phase is not (ContentOpenPhase.Prepared or ContentOpenPhase.Queued)) return Ignore(state, "Not dispatchable.");
        var request = state.Request;
        var current = ContentRelationPolicy.Resolve(catalog, request.ReadSet.PosterId,
            new PosterItem(request.ReadSet.PosterItemId, request.ReadSet.PosterId, request.ReadSet.MembershipId).LayoutItemId);
        if (current is null || current.Witness != request.ReadSet || current.Item.Source != request.Source ||
            current.Item.Target != request.Target ||
            !ContentCapabilityPolicy.Evaluate(current.Item, ContentOperation.Open, transfer).Allowed)
            return FinishBeforeDispatch(state, ContentOpenOutcome.Unavailable);
        return new(state with { Phase = ContentOpenPhase.Running }, true);
    }

    public static ContentOpenTransition FinishBeforeDispatch(ContentOpenState state, ContentOpenOutcome outcome)
    {
        if (state.Phase is not (ContentOpenPhase.Prepared or ContentOpenPhase.Queued)) return Ignore(state, "Dispatch already started.");
        if (outcome is not (ContentOpenOutcome.Busy or ContentOpenOutcome.Rejected or ContentOpenOutcome.Cancelled or
            ContentOpenOutcome.Unavailable or ContentOpenOutcome.Failed or ContentOpenOutcome.TimedOut))
            return Ignore(state, "Cannot claim success before dispatch.");
        return new(state with { Phase = ContentOpenPhase.Completed, Outcome = outcome }, true);
    }

    public static ContentOpenTransition Cancel(ContentOpenState state)
    {
        if (state.Phase == ContentOpenPhase.Completed) return Ignore(state, "Already completed.");
        if (state.Phase == ContentOpenPhase.Running)
            return new(state with { CancellationRequested = true }, !state.CancellationRequested);
        var cancelled = FinishBeforeDispatch(state, ContentOpenOutcome.Cancelled);
        return cancelled with { State = cancelled.State with { CancellationRequested = true } };
    }

    // Expiring a UI wait is not proof of native termination or permission to release a worker slot.
    public static ContentOpenTransition ExpireWait(ContentOpenState state)
    {
        if (state.Phase == ContentOpenPhase.Completed) return Ignore(state, "Already completed.");
        if (state.Phase == ContentOpenPhase.Running)
            return new(state with { WaitExpired = true }, !state.WaitExpired);
        return FinishBeforeDispatch(state, ContentOpenOutcome.TimedOut);
    }

    public static ContentOpenTransition Complete(ContentOpenState state, ContentOpenCompletion completion)
    {
        if (state.Phase != ContentOpenPhase.Running) return Ignore(state, "Not running; late/duplicate result.");
        if (completion is null || completion.ActivationId != state.Request.ActivationId ||
            completion.ProviderId != state.Request.Source.ProviderId || !Enum.IsDefined(completion.Outcome) ||
            !Enum.IsDefined(completion.Evidence) || completion.Outcome == ContentOpenOutcome.None)
            return Ignore(state, "Foreign or invalid result.");
        bool success = completion.Outcome == ContentOpenOutcome.Succeeded;
        if (success == (completion.Evidence == ContentOpenEvidence.None))
            return Ignore(state, "Success needs explicit handoff evidence; other results cannot carry success evidence.");
        var next = state with { Phase = ContentOpenPhase.Completed, Outcome = completion.Outcome };
        var request = state.Request;
        // A real handoff after a cancellation request still happened. Do not rewrite the fact
        // as failure, recreate removed memberships, or update a disposed/rebound visual here.
        ContentOpenFact? fact = success ? new(request.ActivationId, request.ReadSet.ContentItemId,
            request.ReadSet.PosterId, request.ReadSet.PosterItemId, request.Source, request.ReadSet.ItemVersion,
            completion.Evidence, completion.ObservedAt) : null;
        return new(next, true, fact);
    }

    private static ContentOpenTransition Ignore(ContentOpenState state, string reason) => new(state, false, Reason: reason);
}

public enum ContentOpenFactAdmission { Added, Duplicate, Conflict, Invalid }
public static class ContentOpenFactPolicy
{
    // Pure unique-key contract. The future single-authority transaction stores the fact and
    // updates statistics together under UNIQUE(ActivationId), never using an evicting click cache.
    public static ContentOpenFactAdmission Admit(IReadOnlyCollection<ContentOpenFact> existing, ContentOpenFact fact)
    {
        if (fact is null || fact.ActivationId == Guid.Empty || fact.ContentItemId == Guid.Empty ||
            fact.PosterItemId == Guid.Empty || string.IsNullOrWhiteSpace(fact.PosterId) || fact.ItemVersion < 1 ||
            fact.Source is null || string.IsNullOrWhiteSpace(fact.Source.ProviderId) || string.IsNullOrWhiteSpace(fact.Source.Key) ||
            fact.Evidence == ContentOpenEvidence.None || !Enum.IsDefined(fact.Evidence))
            return ContentOpenFactAdmission.Invalid;
        var matches = existing.Where(x => x.ActivationId == fact.ActivationId).ToArray();
        return matches.Length == 0 ? ContentOpenFactAdmission.Added :
            matches.Length == 1 && matches[0] == fact ? ContentOpenFactAdmission.Duplicate : ContentOpenFactAdmission.Conflict;
    }
}
