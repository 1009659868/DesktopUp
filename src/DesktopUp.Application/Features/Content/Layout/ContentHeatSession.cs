using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentHeatApplication(bool Applied, long? RejectedVersion = null, string? Error = null);

// One visual-session owner, confined to the host's dispatcher. This is derived
// state only: it cannot persist author intent, schedule work, or own native input.
public sealed class ContentHeatSession
{
    private ContentHeatProposalBuffer? buffer;
    private ContentHeatProposal? active;
    private IReadOnlyDictionary<string, int>? tiers;
    private string? posterId;
    private long itemSetVersion;
    private bool closed;
    private ContentLayoutDocument? basis;
    private ContentLayoutViewport? lastViewport;

    public bool HasPending => buffer?.HasPending == true;
    public long? ActiveVersion => active?.Version;
    public ContentLayoutDocument? Projection { get; private set; }
    public bool HasContinuousProjection => active?.MappingVersion == ContentLayoutProposals.ContinuousMappingVersion;
    public ContentHeatProposal? ContinuousProposal => HasContinuousProjection ? active : null;

    public bool Offer(ContentHeatProposal proposal) => !closed && buffer?.Offer(proposal) == true;

    public void Reconcile(ContentLayoutDocument author, ContentLayoutViewport viewport)
    {
        if (closed) return;
        if (ReferenceEquals(author, basis) && SameMetrics(viewport, lastViewport)) return;
        basis = author; lastViewport = viewport;
        if (posterId != author.PosterId || itemSetVersion != author.ItemSetVersion)
        {
            buffer?.Invalidate();
            posterId = author.PosterId;
            itemSetVersion = author.ItemSetVersion;
            buffer = new(posterId, itemSetVersion);
            active = null;
            Projection = null;
            tiers = null;
        }
        else if (active is not null)
        {
            try
            {
                var resolved = Resolve(author, active, viewport);
                ContentProjectionValidation.RequireWritable(resolved.Document, viewport);
                Projection = resolved.Document;
                tiers = resolved.Tiers;
            }
            catch (ArgumentException)
            {
                // Preserve the accepted proposal for later author reconciliation,
                // but never publish a projection invalid for the new author basis.
                Projection = null;
                tiers = null;
            }
        }
    }

    // Previewing another membership set must not invalidate the committed state.
    public ContentLayoutDocument ProjectEdit(ContentLayoutDocument author) =>
        active is { } heat && heat.PosterId == author.PosterId && heat.ItemSetVersion == author.ItemSetVersion
            ? Resolve(author, heat, lastViewport ?? new(10000, 10000)).Document : author;

    public ContentHeatApplication ApplyPending(ContentLayoutDocument author, ContentLayoutViewport viewport,
        bool interactionProtected)
    {
        if (closed || buffer?.Take(interactionProtected) is not { } proposal) return new(false);
        try
        {
            if (proposal.AuthorDocumentVersion is { } version && version != author.DocumentVersion)
                throw new ContentLayoutException("Activity proposal was computed from an older author document.");
            var resolved = Resolve(author, proposal, viewport);
            ContentProjectionValidation.RequireWritable(resolved.Document, viewport);
            active = proposal;
            Projection = resolved.Document;
            tiers = resolved.Tiers;
            basis = author; lastViewport = viewport;
            return new(true);
        }
        catch (ArgumentException error)
        {
            // Consume the rejected version, retaining the last accepted state.
            return new(false, proposal.Version, error.Message);
        }
    }

    private ContentHeatResolution Resolve(ContentLayoutDocument author, ContentHeatProposal proposal, ContentLayoutViewport viewport) =>
        proposal.MappingVersion == ContentLayoutProposals.ContinuousMappingVersion
            ? new(ContentHeatSizing.Project(author, proposal, viewport), proposal.Version, new Dictionary<string, int>())
            : ContentLayoutProposals.ResolveHeat(author, proposal, tiers);

    private static bool SameMetrics(ContentLayoutViewport a, ContentLayoutViewport? b) => b is not null &&
        a.WidthDip == b.WidthDip && a.HeightDip == b.HeightDip && a.Scale == b.Scale && a.Spacing == b.Spacing &&
        a.BaseMetricsScale == b.BaseMetricsScale;

    public void Close()
    {
        closed = true;
        buffer?.Invalidate();
        buffer = null;
        active = null;
        Projection = null;
        tiers = null;
    }
}
