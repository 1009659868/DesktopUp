using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentOperationFeedback(bool Succeeded, string Message, ContentOpenOutcome Outcome);

// Expected is the dispatched source identity/version, not the current card.
// A stale observation may be discarded without undoing a recorded open fact.
public sealed record ContentSourceObservation(string ItemId, ContentItem Expected, LocalContentResponse Response);

public sealed record ContentActionResult(ContentOperationFeedback Feedback, ContentSourceObservation? Observation = null);
