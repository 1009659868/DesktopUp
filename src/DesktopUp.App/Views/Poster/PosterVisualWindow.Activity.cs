using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal ContentLayoutViewport ActivityViewport => ContentViewportFor(_bounds);
    internal ContentLayoutSnapshot? ActivityLayout => _contentLayout.Snapshot;
    private bool _activityRefreshRequested = true, _activityWasCollapsed;
    private long _activityRequestVersion;
    private ContentActivityOptions? _activityOptions;
    private bool? _activitySizeEnabled;
    private ContentLayoutMode? _activityMode;
    private ActivityReady? _activityReady;
    private sealed record ActivityReady(ContentHeatProposal Proposal, long EntityVersion,
        DesktopRecoveryGenerationPair Generations, ContentLayoutViewport Viewport);
    private bool _activityGrowing, _activityCalculating, _activityRefreshQueued;
    private bool ActivityConfigurationChanged => _activityOptions != ContentDocument.Activity ||
        _activitySizeEnabled != ContentDocument.AllowHeatDrivenSize || _activityMode != ContentDocument.Mode;

    internal void RequestActivityRefresh()
    {
        if (_disposed || _window is null) return;
        // Dirty once per dispatcher batch. One in-flight calculation plus one latest request
        // prevents parallel opens from publishing obsolete intermediate rankings.
        _activityRefreshRequested = true;
        _activityReady = null;
        _activityRequestVersion = checked(_activityRequestVersion + 1);
        if (_activityRefreshQueued) return;
        _activityRefreshQueued = _window.DispatcherQueue.TryEnqueue(() =>
        {
            _activityRefreshQueued = false;
            NotifyActivitySafePoint();
        });
    }

    internal void NotifyActivitySafePoint()
    {
        if (_activityRefreshRequested || _activityReady is not null || ActivityConfigurationChanged ||
            _activityWasCollapsed != (_authoritativePoster?.IsCollapsed == true))
            QueueContentProposalDrain();
    }

    // Committed statistics enqueue live updates; expansion/settings remain refresh triggers.
    // Releasing an interaction drains existing work, never creates an unrelated ranking.
    private void RefreshActivityAtSafePoint()
    {
        if (_disposed || _window is null || _contentSession.IsSuspended) return;
        var document = ContentDocument;
        if (ActivityConfigurationChanged)
        {
            _activityOptions = document.Activity; _activitySizeEnabled = document.AllowHeatDrivenSize; _activityMode = document.Mode;
            _activityRefreshRequested = true;
            _activityReady = null;
            _activityRequestVersion = checked(_activityRequestVersion + 1);
        }
        // Preview geometry is expanded throughout the animation; use committed posture.
        if (_authoritativePoster?.IsCollapsed == true) { _activityWasCollapsed = true; return; }
        if (_activityWasCollapsed)
        {
            _activityWasCollapsed = false; _activityRefreshRequested = true;
            _activityReady = null;
            _activityRequestVersion = checked(_activityRequestVersion + 1);
        }
        if (_activityGrowing || _activityCalculating || _contentSession.ProposalsProtected || spatial.IsSpatialBusy) return;
        if (_activityReady is { } pending)
        {
            _activityReady = null;
            var ready = pending.Proposal;
            if (ready.Version != _activityRequestVersion || ready.AuthorDocumentVersion != document.DocumentVersion ||
                ready.ItemSetVersion != document.ItemSetVersion || pending.EntityVersion != _posterEntityVersion ||
                pending.Generations != CurrentGenerationPair || !SameActivityViewport(pending.Viewport, ActivityViewport))
                _activityRefreshRequested = true; // Rebase pending work after a gesture/environment change.
            else
            {
                _activityGrowing = true;
                spatial.ApplyActivityGrowth(this, ready, allowed =>
                {
                    _activityGrowing = false;
                    if (allowed && !_disposed && ready.Version == _activityRequestVersion &&
                        ContentDocument.DocumentVersion == ready.AuthorDocumentVersion)
                        OfferContentHeat(ready);
                    // A newer success may have arrived during the frame animation.
                    if (_activityRefreshRequested) NotifyActivitySafePoint();
                });
                return;
            }
        }
        if (!_activityRefreshRequested || document.Mode == ContentLayoutMode.Manual ||
            CurrentGenerationPair is not { } generations) return;
        _activityRefreshRequested = false;
        _activityCalculating = true;
        long version = checked(++_activityRequestVersion);
        long entityVersion = _posterEntityVersion;
        var viewport = ActivityViewport;
        spatial.RequestActivity(this, document, version, entityVersion, proposal =>
        {
            _activityCalculating = false;
            if (_disposed) return;
            if (version == _activityRequestVersion)
            {
                if (entityVersion != _posterEntityVersion || generations != CurrentGenerationPair ||
                    document.DocumentVersion != ContentDocument.DocumentVersion || document.ItemSetVersion != ContentDocument.ItemSetVersion ||
                    !SameActivityViewport(viewport, ActivityViewport))
                    _activityRefreshRequested = true;
                else if (proposal is not null) _activityReady = new(proposal, entityVersion, generations, viewport);
            }
            QueueContentProposalDrain();
        });
    }

    private static bool SameActivityViewport(ContentLayoutViewport a, ContentLayoutViewport b) =>
        a.WidthDip == b.WidthDip && a.HeightDip == b.HeightDip && a.Scale == b.Scale && a.Spacing == b.Spacing &&
        a.BaseMetricsScale == b.BaseMetricsScale && a.ThemeMetricsVersion == b.ThemeMetricsVersion;
}
