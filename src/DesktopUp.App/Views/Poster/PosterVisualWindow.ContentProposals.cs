using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // The session owns admission/buffering/safe-point policy; this adapter only dispatches and paints.
    internal bool OfferContentHeat(ContentHeatProposal proposal)
    {
        var document = ContentDocument;
        bool admissible = !_contentParseReadOnly && !_contentLayout.HasFault && (!_contentReadOnly || IsContentSpaceRestricted);
        if (!_contentSession.OfferHeat(proposal, document, admissible)) return false;
        QueueContentProposalDrain();
        return true;
    }
    internal bool OfferContentTheme(ContentThemeProposal proposal)
    {
        if (!_contentSession.OfferTheme(proposal, ContentDocument, ContentViewportFor(_bounds),
            _presentation.Parameters, _contentReadOnly)) return false;
        QueueContentProposalDrain();
        return true;
    }
    private ContentLayoutDocument EffectiveContentDocument => _contentSession.Effective(ContentDocument);
    internal ContentHeatProposal? ActiveContentHeat => _contentSession.ContinuousHeat;
    private ContentLayoutDocument ProjectContentEdit(ContentLayoutDocument author) => _contentSession.ProjectEdit(author);
    private void ResetContentProposals(ContentLayoutDocument current) =>
        _contentSession.Reconcile(current, ContentViewportFor(_bounds));

    private void QueueContentProposalDrain()
    {
        RefreshActivityAtSafePoint();
        if (_contentSession.SourceRefreshPending && _contentSession.CanRefreshSource) RefreshContentSource();
        if (_window is null || !_contentSession.TryQueue(_presentation.Parameters, out var work)) return;
        if (!_window.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_contentSession.BeginDrain(work) || _window is null) return;
            try { DrainContentProposals(); }
            catch (Exception error)
            { log.Write("warning", "content.proposal_drain_failed", new { PosterId, error.Message }); }
        })) _contentSession.CancelQueued(work);
    }
    internal void NotifyCenterContentInteractionChanged()
    {
        // The center remains the owner of its draft; its explicit notification wakes only this session.
        if (_contentSession.SetCenterDraft(spatial.HasCenterDraft(inputSession.PosterId)) ||
            _activityRefreshRequested || _activityReady is not null || ActivityConfigurationChanged)
            QueueContentProposalDrain();
    }
    private void DrainContentProposals()
    {
        var result = _contentSession.Drain(ContentDocument, ContentViewportFor(_bounds), _presentation.Parameters);
        bool changed = result.Heat.Applied;
        if (result.Heat.RejectedVersion is { } version)
            log.Write("warning", "content.heat_proposal_rejected", new { PosterId, Version = version, Message = result.Heat.Error });
        string? themeError = result.ThemeError;
        if (result.Theme is { } theme && _authoritativePoster is { } poster)
        {
            try
            {
                var desired = poster with { LayoutParametersJson = PosterPresentationPolicy.Serialize(theme, poster.LayoutParametersJson) };
                ApplyPresentation(PosterPresentationPolicy.Resolve(desired));
                changed = true;
            }
            catch (ArgumentException error)
            {
                _contentSession.RejectThemePresentation(_presentation.Parameters);
                themeError = error.Message;
            }
        }
        if (themeError is not null)
            log.Write("warning", "content.theme_proposal_rejected", new { PosterId, Message = themeError });
        if (changed)
        {
            _contentLayout.Invalidate();
            bool published = false;
            try { published = ApplyContentFrame(_bounds); }
            finally
            {
                if (result.Heat.Applied) _contentSession.CompleteHeatPublication(published);
                if (!published) _contentLayout.Invalidate();
            }
            if (published && result.Heat.Applied) StartModelTransition(ActiveContentHeat?.Transition);
            spatial.NotifyContentEditorChanged(PosterId);
        }
    }
    private static void RequireWritableContentProjection(ContentLayoutDocument document, ContentLayoutViewport viewport)
        => ContentProjectionValidation.RequireWritable(document, viewport);
}
