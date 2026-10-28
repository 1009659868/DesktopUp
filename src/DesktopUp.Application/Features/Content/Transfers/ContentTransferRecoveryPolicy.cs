using DesktopUp.Domain;

namespace DesktopUp.Application;

// Independent, versioned journal contract, not a SQLite schema or a disk executor.
public enum TransferItemPhase { Planned, IntentDurable, Executing, FileVerified, IndexCommitted, NoOp }
public enum TransferObjectObservation { Absent, ExpectedObject, DifferentObject, Unknown }
public enum TransferRecoveryAction { WriteIntentFirst, Reinspect, RetryOnlyAfterRevalidation, ReconcileIndex, KeepBothForReview, LocateMissing, PreserveConflict, Done, NoDiskChange }
public sealed record TransferItemEvidence(TransferObjectObservation Source, TransferObjectObservation Destination,
    bool DestinationIntegrityVerified);
public static class ContentTransferRecoveryPolicy
{
    // Neither a native effect nor DragEnd appears here: neither proves file movement.
    // No branch authorizes Delete/Rollback. Ambiguous copies remain recoverable.
    public static TransferRecoveryAction Decide(TransferItemPhase phase, TransferItemEvidence evidence)
    {
        if (!Enum.IsDefined(phase) || !Enum.IsDefined(evidence.Source) || !Enum.IsDefined(evidence.Destination))
            return TransferRecoveryAction.Reinspect;
        if (phase == TransferItemPhase.Planned) return TransferRecoveryAction.WriteIntentFirst;
        if (phase == TransferItemPhase.NoOp) return TransferRecoveryAction.NoDiskChange;
        if (phase == TransferItemPhase.IndexCommitted) return TransferRecoveryAction.Done;
        if (evidence.Source == TransferObjectObservation.Unknown || evidence.Destination == TransferObjectObservation.Unknown)
            return TransferRecoveryAction.Reinspect;
        if (evidence.Source == TransferObjectObservation.DifferentObject || evidence.Destination == TransferObjectObservation.DifferentObject)
            return TransferRecoveryAction.PreserveConflict;
        if (evidence.Destination == TransferObjectObservation.ExpectedObject)
        {
            if (!evidence.DestinationIntegrityVerified) return TransferRecoveryAction.Reinspect;
            return evidence.Source == TransferObjectObservation.Absent
                ? TransferRecoveryAction.ReconcileIndex : TransferRecoveryAction.KeepBothForReview;
        }
        return evidence.Source == TransferObjectObservation.ExpectedObject
            ? TransferRecoveryAction.RetryOnlyAfterRevalidation : TransferRecoveryAction.LocateMissing;
    }

    public static bool CanStartExecution(TransferItemPhase phase, bool leaseAndReadSetCurrent, bool cancellationRequested) =>
        phase == TransferItemPhase.IntentDurable && leaseAndReadSetCurrent && !cancellationRequested;
    public static bool CanCommitIndex(TransferItemPhase phase, TransferItemEvidence evidence, bool readSetCurrent) =>
        (phase is TransferItemPhase.Executing or TransferItemPhase.FileVerified) && readSetCurrent &&
        Decide(phase, evidence) == TransferRecoveryAction.ReconcileIndex;
}

public sealed record ManagedCollectionRegistration(Guid RootId, Collection Collection, ContentCatalogSnapshot Catalog);
public static class ManagedCollectionRegistrationPolicy
{
    // Prepare only. Directory creation and registration must be journaled by the next batch.
    // Existing Reference bindings and contents remain byte-for-byte value-equal.
    public static ManagedCollectionRegistration Prepare(ContentCatalogSnapshot catalog, ManagedStorageRoot root,
        Collection collection, string resolvedParentIdentity)
    {
        if (ManagedStoragePolicy.Evaluate(root) != ManagedStorageDecision.Ready ||
            collection.Kind != ContentCollectionKind.Managed || collection.Ownership != ContentStorageOwnership.Managed ||
            resolvedParentIdentity != root.ObjectIdentity || collection.StorageLocator == root.Locator)
            throw new ArgumentException("A distinct managed child collection under a ready root is required.");
        catalog = ContentCatalogPolicy.Freeze(catalog);
        var existing = catalog.Collections.SingleOrDefault(c => c.Id == collection.Id);
        if (existing is not null && existing != collection)
            throw new ArgumentException("Do not convert an existing collection or rebind references.");
        return new(root.Id, collection, existing is not null ? catalog :
            ContentCatalogPolicy.Freeze(catalog with { Collections = catalog.Collections.Append(collection).ToArray() }));
    }
}

