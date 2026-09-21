using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ContentHeatProposalBuffer? _contentHeatBuffer;
    private ContentHeatProposal? _contentActiveHeat;
    private ContentLayoutDocument? _contentHeatProjection;
    private IReadOnlyDictionary<string,int>? _contentHeatTiers;
    private ContentThemeProposalBuffer? _contentThemeBuffer;
    private bool _contentProposalDrainQueued;
    private long _contentProposalDrainEpoch, _contentHighestThemeVersion;
    private bool ContentProposalsProtected => HasContentGesture || IsEditing || _posterMenu is not null ||
        _transitionLayout is not null || _activeCommandRequestId is not null || _disposed || _contentPresentationSuspended;

    // Future heat/theme owners submit proposals here. No scores, per-item timers or implicit persistence.
    internal bool OfferContentHeat(ContentHeatProposal proposal)
    {
        var document=ContentDocument;
        if(_disposed || _contentReadOnly || proposal.PosterId!=document.PosterId || proposal.ItemSetVersion!=document.ItemSetVersion) return false;
        _contentHeatBuffer??=new(document.PosterId,document.ItemSetVersion);
        if(!_contentHeatBuffer.Offer(proposal)) return false;
        QueueContentProposalDrain(); return true;
    }
    internal bool OfferContentTheme(ContentThemeProposal proposal)
    {
        if(_disposed || _contentReadOnly || proposal is null || proposal.Version<=_contentHighestThemeVersion ||
            PosterPresentationPolicy.Validate(proposal.Parameters) is not null) return false;
        _contentThemeBuffer??=new(_presentation.Parameters);
        var p=proposal.Parameters.Preferences??new();
        try { DesktopUp.Application.ContentLayoutCommandPolicy.ValidateAuthoring(ContentDocument,p.Scale,p.Spacing); }
        catch(ArgumentException) {return false;}
        bool accepted=_contentThemeBuffer.Offer(proposal,ContentProposalsProtected);
        if(accepted) {_contentHighestThemeVersion=proposal.Version;QueueContentProposalDrain();}
        return accepted;
    }
    private ContentLayoutDocument EffectiveContentDocument => _contentDraft ?? _contentHeatProjection ?? ContentDocument;
    private void ResetContentProposals(ContentLayoutDocument? previous,ContentLayoutDocument current)
    {
        if(previous is null || previous.PosterId!=current.PosterId || previous.ItemSetVersion!=current.ItemSetVersion)
        {
            _contentHeatBuffer?.Invalidate();_contentHeatBuffer=new(current.PosterId,current.ItemSetVersion);
            _contentActiveHeat=null;_contentHeatProjection=null;_contentHeatTiers=null;
        }
        else if(_contentActiveHeat is { } active)
        {
            try
            {
                var resolved=ContentLayoutProposals.ResolveHeat(current,active,_contentHeatTiers);
                ContentLayoutSolver.Resolve(resolved.Document,ContentViewportFor(_bounds));
                _contentHeatProjection=resolved.Document;_contentHeatTiers=resolved.Tiers;
            }
            catch(ArgumentException) {_contentHeatProjection=null;_contentHeatTiers=null;}
        }
    }
    // The current presenter applies even paint-only themes by rebuilding its XAML tree.
    // Therefore all theme presentation waits for this safe point, even when the pure buffer
    // can accept paint-only values immediately. No protected capture/layout is reconstructed.
    private void QueueContentProposalDrain()
    {
        if(_disposed || _contentPresentationSuspended || _window is null || _contentProposalDrainQueued ||
            !(_contentHeatBuffer?.HasPending==true || _contentThemeBuffer?.HasPending==true ||
                _contentThemeBuffer is { } pendingTheme && pendingTheme.Current!=_presentation.Parameters)) return;
        long epoch=_contentProposalDrainEpoch;
        _contentProposalDrainQueued=true;
        if(!_window.DispatcherQueue.TryEnqueue(()=>
        {
            if(epoch!=_contentProposalDrainEpoch) return;
            _contentProposalDrainQueued=false;
            if(ContentProposalsProtected || _window is null) return;
            try { DrainContentProposals(); }
            catch(Exception error)
            { log.Write("warning","content.proposal_drain_failed",new{PosterId,error.Message}); }
        })) _contentProposalDrainQueued=false;
    }
    private void DrainContentProposals()
    {
        if(ContentProposalsProtected) return;
        bool changed=false;
        if(_contentHeatBuffer?.Take(false) is { } heat)
        {
            try
            {
                var resolved=ContentLayoutProposals.ResolveHeat(ContentDocument,heat,_contentHeatTiers);
                ContentLayoutSolver.Resolve(resolved.Document,ContentViewportFor(_bounds));
                _contentActiveHeat=heat;_contentHeatProjection=resolved.Document;_contentHeatTiers=resolved.Tiers;
                changed=true;
            }
            catch(ArgumentException error)
            {log.Write("warning","content.heat_proposal_rejected",new{PosterId,heat.Version,error.Message});}
        }
        if(_contentThemeBuffer is { } theme)
        {
            theme.ApplyPending(false);
            if(theme.Current!=_presentation.Parameters && _authoritativePoster is { } poster)
            {
                try
                {
                    // The document may have changed while a metrics proposal was buffered.
                    var preferences=theme.Current.Preferences??new();
                    DesktopUp.Application.ContentLayoutCommandPolicy.ValidateAuthoring(EffectiveContentDocument,preferences.Scale,preferences.Spacing);
                    var desired=poster with {LayoutParametersJson=PosterPresentationPolicy.Serialize(theme.Current,poster.LayoutParametersJson)};
                    ApplyPresentation(PosterPresentationPolicy.Resolve(desired));
                    changed=true;
                }
                catch(ArgumentException error)
                {
                    _contentThemeBuffer=new(_presentation.Parameters);
                    log.Write("warning","content.theme_proposal_rejected",new{PosterId,error.Message});
                }
            }
        }
        if(changed) {_solvedDocument=null;RefreshContentSurface();}
    }
    private void ReleaseContentProposals()
    {
        _contentProposalDrainEpoch=unchecked(_contentProposalDrainEpoch+1);_contentProposalDrainQueued=false;
        _contentHeatBuffer?.Invalidate();_contentHeatBuffer=null;_contentActiveHeat=null;
        _contentHeatProjection=null;_contentHeatTiers=null;_contentThemeBuffer=null;
        _contentHighestThemeVersion=0;
    }
}
