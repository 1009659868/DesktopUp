using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed record ContentCenterProposalWakeReadback(bool CenterOwnsDraft, bool WaitingForCenterRelease,
    bool HeatPending, bool ThemePending, long? ActiveHeatVersion, PosterPresentationParameters ThemeParameters,
    string AuthorJson, string GeometryFingerprint, string LayoutFingerprint, long ViewportGeneration, ContentResolvedPlacement? SelectedSlot);
internal sealed record ContentCenterProposalWakeTicket(string PosterId, long LatestHeatVersion, long LatestThemeVersion,
    ContentCenterProposalWakeReadback Baseline, PosterPresentationParameters ExpectedTheme);

// Isolated-profile dispatcher-step helpers. They offer through production APIs and read the actual
// published composition; they never solve a second layout, force draining, or acquire a timer.
internal sealed partial class PosterVisualWindow
{
    internal ContentCenterProposalWakeReadback ReadCenterProposalWakeForProbe()
    {
        var snapshot=_contentSnapshot ?? throw new InvalidOperationException("Content composition is unavailable.");
        return new(PosterId is { } id && spatial.HasCenterDraft(id), _contentAwaitingCenterRelease,
            _contentHeatSession.HasPending,
            _contentThemeBuffer?.HasPending==true || _contentThemeBuffer is { } theme && theme.Current!=_presentation.Parameters,
            _contentHeatSession.ActiveVersion, _presentation.Parameters, ContentLayoutPolicy.Serialize(ContentDocument),
            StateRequestFingerprint.Compute(new { snapshot.ScrollXDip, snapshot.ScrollYDip,
                items=snapshot.Items.Select(p=>new {p.ItemId,p.ReadOrder,p.Bounds,p.Clip}).ToArray() }),
            StateRequestFingerprint.Compute(snapshot.Items.Select(p=>new {p.ItemId,p.ReadOrder,p.Bounds}).ToArray()),
            snapshot.ViewportGeneration, snapshot.Items.FirstOrDefault(p=>p.ItemId==_selectedContentItem));
    }

    internal ContentCenterProposalWakeTicket QueueCenterProposalWakeForProbe()
    {
        var baseline=ReadCenterProposalWakeForProbe();
        var document=ContentDocument;
        if(!baseline.CenterOwnsDraft || IsEditing || HasContentGesture || _contentReadOnly ||
            baseline.HeatPending || baseline.ThemePending || baseline.ActiveHeatVersion is not null ||
            baseline.SelectedSlot is null || document.Mode!=ContentLayoutMode.Automatic ||
            document.Placements.Count<2 || document.Placements.Any(p=>p.FixedPosition || p.PinnedIndex.HasValue))
            throw new InvalidOperationException("The wake fixture requires a center-owned draft on ordinary unpinned automatic content with no prior heat.");

        var preferences=baseline.ThemeParameters.Preferences ?? new();
        // Small metric changes preserve the ordinary fixture's available capacity. Both proposals
        // differ from the basis and each other, so a premature/old application is observable.
        double latestScale=preferences.Scale>=PosterContentPreferencePolicy.MinimumScale+.1
            ? preferences.Scale-.1 : preferences.Scale+.1;
        var firstTheme=baseline.ThemeParameters with { Preferences=preferences with { Scale=(preferences.Scale+latestScale)/2 } };
        var latestTheme=baseline.ThemeParameters with { Preferences=preferences with { Scale=latestScale } };
        long firstVersion=checked(_contentHighestThemeVersion+1), latestVersion=checked(firstVersion+1);
        ContentHeatProposal Heat(long version,bool latest)=>new(document.PosterId,document.ItemSetVersion,version,
            document.Placements.Select((p,i)=>new ContentHeatItem(p.ItemId,
                latest ? document.Placements.Count-i-1 : i, latest ? .5 : 1)).ToArray());
        if(!OfferContentHeat(Heat(firstVersion,false)) || !OfferContentHeat(Heat(latestVersion,true)) ||
            !OfferContentTheme(new(firstVersion,firstTheme)) || !OfferContentTheme(new(latestVersion,latestTheme)))
            throw new InvalidOperationException("Production proposal admission rejected the wake fixture.");
        return new(document.PosterId,latestVersion,latestVersion,baseline,latestTheme);
    }

    internal void RestoreCenterProposalWakeForProbe(ContentCenterProposalWakeTicket ticket)
    {
        if(ticket.PosterId!=PosterId || spatial.HasCenterDraft(ticket.PosterId) ||
            ContentLayoutPolicy.Serialize(ContentDocument)!=ticket.Baseline.AuthorJson)
            throw new InvalidOperationException("The wake fixture changed author intent or retained the center owner.");
        // Empty neutral heat re-derives the original author.Order; it does not guess an index order.
        if(!OfferContentHeat(new(ticket.PosterId,ContentDocument.ItemSetVersion,checked(ticket.LatestHeatVersion+1),[])) ||
            !OfferContentTheme(new(checked(ticket.LatestThemeVersion+1),ticket.Baseline.ThemeParameters)))
            throw new InvalidOperationException("Production proposal admission rejected wake-fixture restoration.");
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal ContentCenterProposalWakeTicket QueueCenterProposalWakeForProbe(string id) =>
        _windows[id].QueueCenterProposalWakeForProbe();
    internal ContentCenterProposalWakeReadback ReadCenterProposalWakeForProbe(string id) =>
        _windows[id].ReadCenterProposalWakeForProbe();
    internal void RestoreCenterProposalWakeForProbe(string id,ContentCenterProposalWakeTicket ticket) =>
        _windows[id].RestoreCenterProposalWakeForProbe(ticket);
}
