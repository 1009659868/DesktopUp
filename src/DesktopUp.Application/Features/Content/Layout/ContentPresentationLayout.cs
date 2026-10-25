using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>One immutable solver result shared by rendering and hit geometry. Never persisted.</summary>
public sealed record ContentPresentationFrame(ContentLayoutDocument Document, ContentLayoutSnapshot Snapshot,
    ContentLayoutViewport Viewport, ContentBrowseMode BrowseMode, ContentSpaceDecision? Space, string? Fault = null);

public sealed record ContentLayoutSurface(Guid Session, long LayoutGeneration, SpatialEnvironment Environment,
    SpatialRect Bounds, string Metrics, bool Visible, bool Safe, double AuthoredWidth, double AuthoredHeight)
{
    public ContentSpaceWitness Witness(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport) =>
        new(snapshot.PosterId, Session, snapshot.DocumentVersion, snapshot.ItemSetVersion, LayoutGeneration,
            Environment, Bounds, Metrics, viewport.ThemeMetricsVersion, viewport.Generation,
            viewport.WidthDip, viewport.HeightDip, Visible, Safe);
}

/// <summary>Pure per-session layout preparation. Reuses the existing solver and space admission;
/// owns no author state, UI, file IO or scheduling. Native success is still required to publish Space.</summary>
public sealed class ContentPresentationLayout
{
    private ContentLayoutDocument? solvedDocument;
    private long generation;
    public ContentPresentationFrame? Current { get; private set; }
    public ContentSpacePublication Space { get; } = new();
    public ContentBrowseAnchor? EndAnchor { get; set; }
    public long Generation => generation;
    public ContentLayoutSnapshot? Snapshot => Current?.Snapshot;
    public ContentLayoutViewport? Viewport => Current?.Viewport;
    public ContentBrowseMode BrowseMode => Current?.BrowseMode ?? ContentBrowseMode.SingleAxis;
    public ContentSpaceDecision? Candidate => Current?.Space;
    public bool HasFault => Current?.Fault is not null;

    public void DiscardCandidate() { if (Current is not null) Current = Current with { Space = null }; }
    public void Invalidate() => solvedDocument = null;
    public void Clear() { Current = null; EndAnchor = null; Invalidate(); }
    public void Adopt(ContentPresentationFrame frame)
    {
        Current = frame;
        solvedDocument = frame.Document;
    }

    public ContentPresentationFrame Prepare(ContentLayoutDocument document, ContentLayoutViewport viewport,
        ContentLayoutSurface? surface, bool gesture, bool transition, bool collapsed, bool safePoint)
    {
        if (ReferenceEquals(solvedDocument, document) && Viewport == viewport && Current is not null && EndAnchor is null)
            return Current;
        bool geometryChanged = HasFault || !ReferenceEquals(solvedDocument, document) ||
            Viewport?.WidthDip != viewport.WidthDip || Viewport?.HeightDip != viewport.HeightDip ||
            Viewport?.Scale != viewport.Scale || Viewport?.Spacing != viewport.Spacing ||
            Viewport?.BaseMetricsScale != viewport.BaseMetricsScale ||
            Viewport?.ThemeMetricsVersion != viewport.ThemeMetricsVersion;
        viewport = viewport with { Generation = checked(++generation) };
        ContentLayoutSnapshot next;
        ContentSpaceDecision? candidate = null;
        ContentBrowseMode mode = BrowseMode;
        string? fault = null;
        try
        {
            var anchor = EndAnchor ?? (geometryChanged && !gesture && !HasFault && Current is not null
                ? ContentLayoutFlow.CaptureAnchor(Current.Snapshot, Current.Viewport, mode) : null);
            EndAnchor = null;
            next = geometryChanged || Snapshot is null ? ContentLayoutSolver.Resolve(document, viewport)
                : ContentLayoutSolver.Reclip(Snapshot, viewport, mode);
            if (!transition && !collapsed && surface is not null)
            {
                var witness = surface.Witness(next, viewport);
                var cause = ContentSpaceOwnerAdmission.Classify(witness, next, Space.Published,
                    next.FlowAxis == ContentFlowAxis.Vertical ? surface.AuthoredWidth : surface.AuthoredHeight);
                candidate = ContentSpacePolicy.Evaluate(new(witness, next, viewport, cause, safePoint, anchor),
                    witness, Space.Published);
            }
            if (candidate?.Disposition == ContentSpaceDisposition.PendingSafePoint)
            {
                if (Space.Published?.Witness is { } previous && previous.Environment == surface?.Environment &&
                    previous.EffectiveFrame == surface.Bounds && Current is not null)
                {
                    EndAnchor = anchor; Invalidate();
                    return Current;
                }
                throw new ContentLayoutException("空间恢复尚未到达安全点，旧命中已停用。");
            }
            mode = candidate?.BrowseMode ?? (transition &&
                next.SpaceStatus == ContentLayoutSpaceStatus.InsufficientCrossAxis && Space.Published?.IsRestricted == true
                ? ContentBrowseMode.ReadOnlyOverflow : ContentBrowseMode.SingleAxis);
            if (candidate?.Offsets is { } accepted)
                next = ContentLayoutSolver.Reclip(next, viewport with
                { ScrollXDip = accepted.ScrollXDip, ScrollYDip = accepted.ScrollYDip }, mode);
            else if (anchor is not null)
            {
                var restored = ContentLayoutFlow.RestoreAnchor(next, viewport, anchor, mode);
                next = ContentLayoutSolver.Reclip(next, viewport with
                { ScrollXDip = restored.ScrollXDip, ScrollYDip = restored.ScrollYDip }, mode);
            }
        }
        catch (ArgumentException error)
        {
            fault = error.Message;
            candidate = null; mode = ContentBrowseMode.SingleAxis;
            next = ContentLayoutSolver.Resolve(ContentLayoutSamples.Equal(document.PosterId, 0), viewport);
        }
        solvedDocument = document;
        return Current = new(document, next,
            viewport with { ScrollXDip = next.ScrollXDip, ScrollYDip = next.ScrollYDip }, mode, candidate, fault);
    }
}
