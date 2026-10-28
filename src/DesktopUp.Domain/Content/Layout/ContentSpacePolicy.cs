namespace DesktopUp.Domain;

public enum ContentSpaceCause
{
    AuthoritativeRefresh, PassivePhysicalEnvironmentChange, ColdStartPhysicalShortage,
    ActiveResize, HeatProposal, PresentationProposal, OuterCollision
}

public enum ContentSpaceDisposition { Publish, RejectCandidate, DeferToOuterOwner, PendingSafePoint, Unavailable, Stale }
public enum ContentSpaceReason
{
    Normal, PassivePhysicalShortage, ColdStartPhysicalShortage, ContinuingPhysicalShortage, Recovered,
    ActiveResizeBelowMinimum, HeatProposalBelowMinimum, PresentationProposalBelowMinimum,
    OuterSpaceConstraint, LegacyConstraintConflict, UnqualifiedShortage, HiddenOrRemoved,
    UnsafeSurface, InvalidObservation, StaleWitness, AwaitingSafePoint
}

/// <summary>A witness obtained from the existing surface/outer-space owner, current authoring
/// projection and content metrics. Unrelated catalog revisions or titles are deliberately absent.
/// Visibility and safe-surface facts come from the current owner; this type grants no placement rights.</summary>
public sealed record ContentSpaceWitness(string PosterId, Guid SurfaceSessionId,
    long DocumentVersion, long ItemSetVersion, long LayoutGeneration,
    SpatialEnvironment Environment, SpatialRect EffectiveFrame, string MetricsVersion,
    long ThemeMetricsVersion, long ViewportGeneration, double ContentWidthDip, double ContentHeightDip,
    bool IsVisible = true, bool HasSafeSurface = true);

/// <summary>ColdStartPhysicalShortage is an explicit admission supplied by the outer owner after
/// current physical-space validation. It must never be inferred from a solver shortage flag.</summary>
public sealed record ContentSpaceRequest(ContentSpaceWitness Witness, ContentLayoutSnapshot? Snapshot,
    ContentLayoutViewport? Viewport, ContentSpaceCause Cause, bool AtSafePoint = true,
    ContentBrowseAnchor? Anchor = null);

/// <summary>A pure publication decision. Pending/rejected candidates contain no new drawable frame;
/// the caller retains its already-published frame until a new decision is accepted. Nothing is saved.</summary>
public sealed record ContentSpaceDecision(ContentSpaceDisposition Disposition, ContentSpaceReason Reason,
    ContentSpaceWitness Witness, ContentFlowAxis? FlowAxis, ContentBrowseMode? BrowseMode,
    ContentBrowseOffsets? Offsets, bool IsReadOnly, bool CancelContentGesture, bool RecoveryEligible)
{
    public bool IsRestricted => Disposition == ContentSpaceDisposition.Publish && BrowseMode == ContentBrowseMode.ReadOnlyOverflow;
    public bool RetainPublishedFrame => Disposition is ContentSpaceDisposition.PendingSafePoint or ContentSpaceDisposition.RejectCandidate;
}

/// <summary>Admission and recovery policy only: no window writes, collision solver, timer, database,
/// gesture lease or second space owner. Native publication must still use the existing safe channel.</summary>
public static class ContentSpacePolicy
{
    public static ContentSpaceDecision Evaluate(ContentSpaceRequest request, ContentSpaceWitness latest,
        ContentSpaceDecision? previous = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(latest);
        // Only the last published decision is a valid continuity basis. In particular a pending
        // recovery is not a new published frame and cannot erase the prior restricted state.
        if (previous is not null && previous.Disposition != ContentSpaceDisposition.Publish)
            return Decision(ContentSpaceDisposition.Unavailable, ContentSpaceReason.InvalidObservation);
        if (request.Witness != latest || IsOlderThanPublished(request.Witness, previous))
            return Decision(ContentSpaceDisposition.Stale, ContentSpaceReason.StaleWitness);
        if (!latest.IsVisible)
            return Decision(ContentSpaceDisposition.Unavailable, ContentSpaceReason.HiddenOrRemoved, cancel: true);
        if (!latest.HasSafeSurface || !SafePhysicalSurface(latest))
            return Decision(ContentSpaceDisposition.Unavailable, ContentSpaceReason.UnsafeSurface, cancel: true);
        if (!ValidObservation(request))
            return Decision(ContentSpaceDisposition.Unavailable, ContentSpaceReason.InvalidObservation, cancel: true);
        var snapshot = request.Snapshot!;
        var viewport = request.Viewport!;
        bool invalidatesPriorGeometry = previous is not null && GeometryChanged(previous.Witness, latest);
        if (request.Cause == ContentSpaceCause.OuterCollision)
            return Decision(ContentSpaceDisposition.DeferToOuterOwner, ContentSpaceReason.OuterSpaceConstraint, cancel: invalidatesPriorGeometry);
        if (snapshot.SpaceStatus == ContentLayoutSpaceStatus.LegacyConstraintConflict)
            return Decision(IsProposal(request.Cause) ? ContentSpaceDisposition.RejectCandidate : ContentSpaceDisposition.Unavailable,
                ContentSpaceReason.LegacyConstraintConflict, cancel: invalidatesPriorGeometry);
        if (snapshot.SpaceStatus == ContentLayoutSpaceStatus.Normal)
        {
            bool recovering = previous?.IsRestricted == true;
            if (recovering && !request.AtSafePoint)
                return Decision(ContentSpaceDisposition.PendingSafePoint, ContentSpaceReason.AwaitingSafePoint,
                    cancel: invalidatesPriorGeometry, recovery: true);
            return Publish(ContentBrowseMode.SingleAxis, recovering ? ContentSpaceReason.Recovered : ContentSpaceReason.Normal,
                readOnly: false, cancel: invalidatesPriorGeometry, recovery: recovering);
        }
        if (IsProposal(request.Cause))
            return Decision(ContentSpaceDisposition.RejectCandidate, request.Cause switch
            {
                ContentSpaceCause.ActiveResize => ContentSpaceReason.ActiveResizeBelowMinimum,
                ContentSpaceCause.HeatProposal => ContentSpaceReason.HeatProposalBelowMinimum,
                _ => ContentSpaceReason.PresentationProposalBelowMinimum
            });

        bool sameSurface = previous is not null && SameSurface(previous.Witness, latest);
        bool continuing = sameSurface && previous!.IsRestricted;
        bool physicalChange = sameSurface && request.Cause == ContentSpaceCause.PassivePhysicalEnvironmentChange &&
            PhysicalEnvironmentChanged(previous!.Witness.Environment, latest.Environment) &&
            previous.FlowAxis == snapshot.FlowAxis && CrossLength(latest, snapshot.FlowAxis) < CrossLength(previous.Witness, snapshot.FlowAxis);
        bool coldStart = previous is null && request.Cause == ContentSpaceCause.ColdStartPhysicalShortage;
        if (!continuing && !physicalChange && !coldStart)
            return Decision(ContentSpaceDisposition.Unavailable, ContentSpaceReason.UnqualifiedShortage, cancel: invalidatesPriorGeometry);
        return Publish(ContentBrowseMode.ReadOnlyOverflow,
            continuing ? ContentSpaceReason.ContinuingPhysicalShortage : coldStart ? ContentSpaceReason.ColdStartPhysicalShortage : ContentSpaceReason.PassivePhysicalShortage,
            readOnly: true, cancel: true, recovery: false);

        ContentSpaceDecision Decision(ContentSpaceDisposition disposition, ContentSpaceReason reason, bool cancel = false, bool recovery = false) =>
            new(disposition, reason, latest, request.Snapshot?.FlowAxis, null, null, true, cancel, recovery);

        ContentSpaceDecision Publish(ContentBrowseMode mode, ContentSpaceReason reason, bool readOnly, bool cancel, bool recovery)
        {
            try
            {
                var offsets = request.Anchor is null
                    ? ContentLayoutFlow.NormalizeOffsets(snapshot, viewport, mode)
                    : ContentLayoutFlow.RestoreAnchor(snapshot, viewport, request.Anchor, mode);
                return new(ContentSpaceDisposition.Publish, reason, latest, snapshot.FlowAxis, mode, offsets, readOnly, cancel, recovery);
            }
            catch (ArgumentException)
            {
                return Decision(ContentSpaceDisposition.Unavailable, ContentSpaceReason.InvalidObservation, cancel: true);
            }
        }
    }

    private static bool ValidObservation(ContentSpaceRequest request)
    {
        var witness = request.Witness;
        var snapshot = request.Snapshot;
        var viewport = request.Viewport;
        if (witness is null || !Enum.IsDefined(request.Cause) || string.IsNullOrWhiteSpace(witness.PosterId) ||
            witness.SurfaceSessionId == Guid.Empty || witness.DocumentVersion < 1 || witness.ItemSetVersion < 1 ||
            witness.LayoutGeneration < 0 || witness.ThemeMetricsVersion < 1 || witness.ViewportGeneration < 0 ||
            string.IsNullOrWhiteSpace(witness.MetricsVersion) || snapshot is null || viewport is null ||
            snapshot.PosterId != witness.PosterId || snapshot.DocumentVersion != witness.DocumentVersion ||
            snapshot.ItemSetVersion != witness.ItemSetVersion || snapshot.ViewportGeneration != witness.ViewportGeneration ||
            snapshot.ThemeMetricsVersion != witness.ThemeMetricsVersion || viewport.Generation != witness.ViewportGeneration ||
            viewport.ThemeMetricsVersion != witness.ThemeMetricsVersion || viewport.WidthDip != witness.ContentWidthDip ||
            viewport.HeightDip != witness.ContentHeightDip || !Enum.IsDefined(snapshot.SpaceStatus)) return false;
        try
        {
            // Ordinary normalization validates the finite geometry without granting overflow mode.
            _ = ContentLayoutFlow.NormalizeOffsets(snapshot, viewport);
            double cross = snapshot.FlowAxis == ContentFlowAxis.Vertical ? viewport.WidthDip : viewport.HeightDip;
            if (!double.IsFinite(snapshot.MinimumCrossAxisDip) || snapshot.MinimumCrossAxisDip < 0 ||
                !double.IsFinite(snapshot.AvailableCrossAxisDip) || Math.Abs(snapshot.AvailableCrossAxisDip - cross) > 1e-7) return false;
            if (snapshot.SpaceStatus == ContentLayoutSpaceStatus.Normal && snapshot.MinimumCrossAxisDip > cross + 1e-7) return false;
            if (snapshot.SpaceStatus == ContentLayoutSpaceStatus.InsufficientCrossAxis &&
                (snapshot.Items.Count == 0 || snapshot.MinimumCrossAxisDip <= cross + 1e-7)) return false;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool SafePhysicalSurface(ContentSpaceWitness witness) => witness.Environment is { IsValid: true } &&
        witness.EffectiveFrame.IsValid && witness.Environment.WorkArea.Contains(witness.EffectiveFrame) &&
        double.IsFinite(witness.ContentWidthDip) && witness.ContentWidthDip > 0 &&
        double.IsFinite(witness.ContentHeightDip) && witness.ContentHeightDip > 0 &&
        witness.ContentWidthDip <= witness.EffectiveFrame.Width * 96d / witness.Environment.DpiX + 1e-7 &&
        witness.ContentHeightDip <= witness.EffectiveFrame.Height * 96d / witness.Environment.DpiY + 1e-7;

    private static bool IsProposal(ContentSpaceCause cause) => cause is ContentSpaceCause.ActiveResize or
        ContentSpaceCause.HeatProposal or ContentSpaceCause.PresentationProposal;
    private static bool SameSurface(ContentSpaceWitness a, ContentSpaceWitness b) =>
        a.PosterId == b.PosterId && a.SurfaceSessionId == b.SurfaceSessionId && a.Environment is not null && b.Environment is not null &&
        a.Environment.RuntimeSessionId == b.Environment.RuntimeSessionId;
    private static bool PhysicalEnvironmentChanged(SpatialEnvironment a, SpatialEnvironment b) =>
        a.WorkspaceId != b.WorkspaceId || a.WorkArea != b.WorkArea || a.DpiX != b.DpiX || a.DpiY != b.DpiY || a.DisplayGeneration != b.DisplayGeneration;
    private static double CrossLength(ContentSpaceWitness witness, ContentFlowAxis axis) =>
        axis == ContentFlowAxis.Vertical ? witness.ContentWidthDip : witness.ContentHeightDip;
    private static bool GeometryChanged(ContentSpaceWitness a, ContentSpaceWitness b) => !SameSurface(a, b) ||
        a.DocumentVersion != b.DocumentVersion || a.ItemSetVersion != b.ItemSetVersion || a.LayoutGeneration != b.LayoutGeneration ||
        a.Environment != b.Environment || a.EffectiveFrame != b.EffectiveFrame || a.MetricsVersion != b.MetricsVersion ||
        a.ThemeMetricsVersion != b.ThemeMetricsVersion || a.ContentWidthDip != b.ContentWidthDip || a.ContentHeightDip != b.ContentHeightDip;
    private static bool IsOlderThanPublished(ContentSpaceWitness witness, ContentSpaceDecision? previous) =>
        previous is not null && SameSurface(witness, previous.Witness) &&
        (witness.DocumentVersion < previous.Witness.DocumentVersion || witness.ItemSetVersion < previous.Witness.ItemSetVersion ||
         witness.LayoutGeneration < previous.Witness.LayoutGeneration ||
         witness.ViewportGeneration < previous.Witness.ViewportGeneration ||
         witness.Environment.DisplayGeneration < previous.Witness.Environment.DisplayGeneration ||
         witness.Environment.ShellGeneration < previous.Witness.Environment.ShellGeneration);
}
