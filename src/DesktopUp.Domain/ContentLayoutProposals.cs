namespace DesktopUp.Domain;

public sealed record ContentHeatItem(string ItemId, int Rank, double RelativeScale, string Reason = "injected");
public sealed record ContentHeatProposal(string PosterId, long ItemSetVersion, long Version,
    IReadOnlyList<ContentHeatItem> Items, int MappingVersion = 1);
public sealed record ContentHeatResolution(ContentLayoutDocument Document, long ProposalVersion,
    IReadOnlyDictionary<string, int> Tiers);
public enum ContentThemeChange { None, PaintOnly, Metrics }

/// <summary>Upstream adapters only: never computes usage, changes statistics or persists a projected document.</summary>
public static class ContentLayoutProposals
{
    public const int MappingVersion = 1;
    public const double Hysteresis = .05;
    public static IReadOnlyList<double> TierScales { get; } = Array.AsReadOnly(new[] { .5, .75, 1d, 1.5, 3d });

    public static int Quantize(double relativeScale, int? previousTier = null)
    {
        if (!double.IsFinite(relativeScale) || relativeScale is < .5 or > 3 || previousTier is < 0 or > 4)
            throw new ContentLayoutException("Heat scale/tier is outside the length-multiplier contract.");
        if (previousTier is { } prior)
        {
            double low = prior == 0 ? .5 : (TierScales[prior - 1] + TierScales[prior]) / 2 - Hysteresis;
            double high = prior == 4 ? 3 : (TierScales[prior] + TierScales[prior + 1]) / 2 + Hysteresis;
            if (relativeScale >= low && relativeScale <= high) return prior;
        }
        int best = 0;
        for (int i = 1; i < TierScales.Count; i++)
            if (Math.Abs(relativeScale - TierScales[i]) < Math.Abs(relativeScale - TierScales[best])) best = i;
        return best;
    }

    public static ContentHeatResolution ResolveHeat(ContentLayoutDocument authorIntent, ContentHeatProposal proposal,
        IReadOnlyDictionary<string, int>? previousTiers = null)
    {
        if (ContentLayoutPolicy.Validate(authorIntent) is { } error) throw new ContentLayoutException(error);
        Validate(proposal);
        if (proposal.PosterId != authorIntent.PosterId || proposal.ItemSetVersion != authorIntent.ItemSetVersion)
            throw new ContentLayoutException("Heat proposal belongs to a stale/different item set.");
        var ids = authorIntent.Placements.Select(p => p.ItemId).ToHashSet(StringComparer.Ordinal);
        if (proposal.Items.Any(p => !ids.Contains(p.ItemId))) throw new ContentLayoutException("Heat proposal contains a foreign/removed item.");
        var heats = proposal.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var tiers = new Dictionary<string, int>(StringComparer.Ordinal);
        var placements = authorIntent.Placements.Select(p =>
        {
            if (!heats.TryGetValue(p.ItemId, out var h)) return p;
            int tier = Quantize(h.RelativeScale, previousTiers is not null && previousTiers.TryGetValue(p.ItemId, out int old) ? old : null);
            tiers[p.ItemId] = tier;
            if (authorIntent.Mode == ContentLayoutMode.Manual) return p;
            // The legacy injected heat proposal remains tiered; the real heat producer is deferred.
            double Scale(double span) => Math.Clamp(Math.Round(span * TierScales[tier], MidpointRounding.AwayFromZero), 1, ContentLayoutPolicy.MaximumSpan);
            bool resize = authorIntent.AllowHeatDrivenSize && !p.ManualSize;
            return p with { Order = h.Rank, ColumnSpan = resize ? Scale(p.ColumnSpan) : p.ColumnSpan,
                RowSpan = resize ? Scale(p.RowSpan) : p.RowSpan };
        }).ToArray();
        // Always re-derive from AUTHOR intent; passing the previous effective document would compound heat.
        return new(ContentLayoutPolicy.Freeze(authorIntent with { Placements = placements }), proposal.Version,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(tiers));
    }

    public static ContentThemeChange ClassifyThemeChange(PosterPresentationParameters before, PosterPresentationParameters after)
    {
        if (PosterPresentationPolicy.Validate(after) is { } error) throw new ContentLayoutException(error);
        if (before == after) return ContentThemeChange.None;
        return before.Content == after.Content && before.Preferences == after.Preferences ? ContentThemeChange.PaintOnly : ContentThemeChange.Metrics;
    }

    internal static void Validate(ContentHeatProposal proposal)
    {
        if (proposal is null || string.IsNullOrWhiteSpace(proposal.PosterId) || proposal.ItemSetVersion < 1 || proposal.Version < 1 ||
            proposal.MappingVersion != MappingVersion || proposal.Items is null || proposal.Items.Count > ContentLayoutPolicy.MaximumItems)
            throw new ContentLayoutException("Unsupported/invalid heat proposal.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in proposal.Items)
            if (item is null || string.IsNullOrWhiteSpace(item.ItemId) || !item.ItemId.StartsWith(proposal.PosterId + ":", StringComparison.Ordinal) ||
                !ids.Add(item.ItemId) || item.Rank < 0 || !double.IsFinite(item.RelativeScale) || item.RelativeScale is < .5 or > 3 || item.Reason?.Length > 512)
                throw new ContentLayoutException("Invalid heat item.");
    }
}

/// <summary>Single latest immutable proposal per poster/item-set. Interaction protection never creates a queue of reflows.</summary>
public sealed class ContentHeatProposalBuffer(string posterId, long itemSetVersion)
{
    private string _posterId = posterId;
    private long _itemSetVersion = itemSetVersion, _highestVersion;
    private ContentHeatProposal? _pending;
    public bool HasPending => _pending is not null;
    public bool Offer(ContentHeatProposal proposal)
    {
        ContentLayoutProposals.Validate(proposal);
        if (proposal.PosterId != _posterId || proposal.ItemSetVersion != _itemSetVersion || proposal.Version <= _highestVersion) return false;
        _highestVersion = proposal.Version;
        _pending = proposal with { Items = Array.AsReadOnly(proposal.Items.ToArray()) }; return true;
    }
    public ContentHeatProposal? Take(bool interactionProtected)
    {
        if (interactionProtected) return null;
        var proposal = _pending; _pending = null; return proposal;
    }
    public void Reset(string nextPosterId, long nextItemSetVersion)
    {
        _posterId = nextPosterId; _itemSetVersion = nextItemSetVersion; _highestVersion = 0; _pending = null;
    }
    public void Invalidate() { _pending = null; _posterId = ""; }
}

public sealed record ContentThemeProposal(long Version, PosterPresentationParameters Parameters);
/// <summary>Paint may update without reflow; metric changes wait for a safe point. Invalid themes retain last-good.</summary>
public sealed class ContentThemeProposalBuffer
{
    private ContentThemeProposal? _pending;
    private long _highestVersion;
    public ContentThemeProposalBuffer(PosterPresentationParameters initial)
    {
        if (PosterPresentationPolicy.Validate(initial) is { } error) throw new ContentLayoutException(error);
        Current = initial;
    }
    public PosterPresentationParameters Current { get; private set; }
    public bool HasPending => _pending is not null;
    public bool Offer(ContentThemeProposal proposal, bool interactionProtected)
    {
        if (proposal.Version <= _highestVersion || PosterPresentationPolicy.Validate(proposal.Parameters) is not null) return false;
        var change = ContentLayoutProposals.ClassifyThemeChange(Current, proposal.Parameters);
        _highestVersion = proposal.Version;
        if (interactionProtected && change == ContentThemeChange.Metrics) _pending = proposal;
        else { Current = proposal.Parameters; _pending = null; }
        return true;
    }
    public bool ApplyPending(bool interactionProtected)
    {
        if (interactionProtected || _pending is null) return false;
        Current = _pending.Parameters; _pending = null; return true;
    }
}
