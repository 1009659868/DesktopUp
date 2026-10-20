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

    public bool HasPending => buffer?.HasPending == true;
    public long? ActiveVersion => active?.Version;
    public ContentLayoutDocument? Projection { get; private set; }

    public bool Offer(ContentHeatProposal proposal) => !closed && buffer?.Offer(proposal) == true;

    public void Reconcile(ContentLayoutDocument author, ContentLayoutViewport viewport)
    {
        if (closed) return;
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
                var resolved = ContentLayoutProposals.ResolveHeat(author, active, tiers);
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
            ? ContentLayoutProposals.ResolveHeat(author, heat, tiers).Document : author;

    public ContentHeatApplication ApplyPending(ContentLayoutDocument author, ContentLayoutViewport viewport,
        bool interactionProtected)
    {
        if (closed || buffer?.Take(interactionProtected) is not { } proposal) return new(false);
        try
        {
            var resolved = ContentLayoutProposals.ResolveHeat(author, proposal, tiers);
            ContentProjectionValidation.RequireWritable(resolved.Document, viewport);
            active = proposal;
            Projection = resolved.Document;
            tiers = resolved.Tiers;
            return new(true);
        }
        catch (ArgumentException error)
        {
            // Consume the rejected version, retaining the last accepted state.
            return new(false, proposal.Version, error.Message);
        }
    }

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
