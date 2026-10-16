using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private readonly ContentHeatSession _contentHeatSession = new();
    private ContentThemeProposalBuffer? _contentThemeBuffer;
    private bool _contentProposalDrainQueued;
    private bool _contentAwaitingCenterRelease;
    private long _contentProposalDrainEpoch, _contentHighestThemeVersion;
    private bool ContentProposalsProtected => HasFileAction || HasContentGesture || _incomingPreview is not null || _fileDrop?.IsHovering == true || IsEditing || PosterId is { } id && spatial.HasCenterDraft(id) || _posterMenu is not null ||
        _transitionLayout is not null || _activeCommandRequestId is not null || _disposed || _contentPresentationSuspended || IsContentSpaceRestricted;

    // Future heat/theme owners submit proposals here. No scores, per-item timers or implicit persistence.
    internal bool OfferContentHeat(ContentHeatProposal proposal)
    {
        var document=ContentDocument;
        if(_disposed || _contentParseReadOnly || _contentProjectionFault || (_contentReadOnly && !IsContentSpaceRestricted) || proposal.PosterId!=document.PosterId || proposal.ItemSetVersion!=document.ItemSetVersion) return false;
        if(!_contentHeatSession.Offer(proposal)) return false;
        QueueContentProposalDrain(); return true;
    }
    internal bool OfferContentTheme(ContentThemeProposal proposal)
    {
        if(_disposed || _contentReadOnly || proposal is null || proposal.Version<=_contentHighestThemeVersion ||
            PosterPresentationPolicy.Validate(proposal.Parameters) is not null) return false;
        _contentThemeBuffer??=new(_presentation.Parameters);
        var p=proposal.Parameters.Preferences??new();
        try
        {
            DesktopUp.Application.ContentLayoutCommandPolicy.ValidateAuthoring(ContentDocument,p.Scale,p.Spacing);
            RequireWritableContentProjection(EffectiveContentDocument, ContentViewportFor(_bounds) with { Scale = p.Scale, Spacing = p.Spacing });
        }
        catch(ArgumentException) {return false;}
        bool accepted=_contentThemeBuffer.Offer(proposal,ContentProposalsProtected);
        if(accepted) {_contentHighestThemeVersion=proposal.Version;QueueContentProposalDrain();}
        return accepted;
    }
    private ContentLayoutDocument EffectiveContentDocument => _contentDraftProjection ?? _contentDraft ?? _contentHeatSession.Projection ?? ContentDocument;
    // A membership-changing intent is validated against its new author layout, never against
    // heat for the old set. Keep the old accepted heat untouched until the CAS actually commits.
    private ContentLayoutDocument ProjectContentEdit(ContentLayoutDocument author) => _contentHeatSession.ProjectEdit(author);
    private void ResetContentProposals(ContentLayoutDocument current) =>
        _contentHeatSession.Reconcile(current, ContentViewportFor(_bounds));
    // The current presenter applies even paint-only themes by rebuilding its XAML tree.
    // Therefore all theme presentation waits for this safe point, even when the pure buffer
    // can accept paint-only values immediately. No protected capture/layout is reconstructed.
    private void QueueContentProposalDrain()
    {
        if (_contentSourceRefreshPending && _transitionLayout is null && _activeCommandRequestId is null &&
            !_disposed && !_contentPresentationSuspended) RefreshContentSource();
        if(_disposed || _contentPresentationSuspended || _window is null || _contentProposalDrainQueued ||
            !(_contentHeatSession.HasPending || _contentThemeBuffer?.HasPending==true ||
                _contentThemeBuffer is { } pendingTheme && pendingTheme.Current!=_presentation.Parameters)) return;
        long epoch=_contentProposalDrainEpoch;
        _contentProposalDrainQueued=true;
        if(!_window.DispatcherQueue.TryEnqueue(()=>
        {
            if(epoch!=_contentProposalDrainEpoch) return;
            _contentProposalDrainQueued=false;
            if(ContentProposalsProtected || _window is null)
            {
                // A center draft may outlive this dispatcher turn. Its explicit ownership-change
                // notification is the safe-point wake-up; do not poll or drain while it owns input.
                if(_window is not null && PosterId is { } id && spatial.HasCenterDraft(id))
                    _contentAwaitingCenterRelease=true;
                return;
            }
            _contentAwaitingCenterRelease=false;
            try { DrainContentProposals(); }
            catch(Exception error)
            { log.Write("warning","content.proposal_drain_failed",new{PosterId,error.Message}); }
        })) _contentProposalDrainQueued=false;
    }
    internal void NotifyCenterContentInteractionChanged()
    {
        if(!_contentAwaitingCenterRelease || PosterId is not { } id || spatial.HasCenterDraft(id)) return;
        _contentAwaitingCenterRelease=false;
        // Reuse the ordinary gate and revalidation. Other owners may still defer this proposal.
        QueueContentProposalDrain();
    }
    private void DrainContentProposals()
    {
        if(ContentProposalsProtected) return;
        var heat = _contentHeatSession.HasPending
            ? _contentHeatSession.ApplyPending(ContentDocument, ContentViewportFor(_bounds), interactionProtected: false)
            : new ContentHeatApplication(false);
        bool changed = heat.Applied;
        if (heat.RejectedVersion is { } version)
            log.Write("warning", "content.heat_proposal_rejected", new { PosterId, Version = version, Message = heat.Error });
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
                    RequireWritableContentProjection(EffectiveContentDocument, ContentViewportFor(_bounds) with
                        { Scale = preferences.Scale, Spacing = preferences.Spacing });
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
        if(changed)
        {
            _solvedDocument=null;RefreshContentSurface();
            spatial.NotifyContentEditorChanged(PosterId);
        }
    }
    private void ReleaseContentProposals()
    {
        _contentProposalDrainEpoch=unchecked(_contentProposalDrainEpoch+1);_contentProposalDrainQueued=false;
        _contentAwaitingCenterRelease=false;
        _contentHeatSession.Close();_contentThemeBuffer=null;
        _contentHighestThemeVersion=0;
    }
    private static void RequireWritableContentProjection(ContentLayoutDocument document, ContentLayoutViewport viewport)
        => ContentProjectionValidation.RequireWritable(document, viewport);
}
