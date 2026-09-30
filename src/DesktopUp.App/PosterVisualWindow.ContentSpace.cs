using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private readonly Guid _contentSurfaceSession = Guid.NewGuid();
    private readonly ContentSpacePublication _contentSpacePublication = new();
    private SpatialEnvironment? _contentSpaceEnvironment;
    private ContentSpaceDecision? _contentSpaceCandidate;
    private long _contentSpaceLayoutGeneration, _contentSpaceEpoch;
    private bool _contentEnvironmentPublishing;
    private ContentBrowseMode _contentBrowseMode = ContentBrowseMode.SingleAxis;
    private bool IsContentSpaceRestricted => _contentBrowseMode == ContentBrowseMode.ReadOnlyOverflow;
    private Grid? _contentSpaceNavigation;

    private void SetContentEnvironment(DisplayMonitorSnapshot monitor, long display, long shell)
    {
        var next = spatial.ContentEnvironment(monitor, display, shell);
        if (_contentSpaceEnvironment == next) return;
        if (_contentSnapshot is not null && _contentViewport is not null && !_contentProjectionFault)
            _contentEndAnchor = ContentLayoutFlow.CaptureAnchor(_contentSnapshot, _contentViewport, _contentBrowseMode);
        CancelContentGesture("physical-environment-changed");
        _contentActivation.Reset();
        _contentSpaceEpoch = checked(_contentSpaceEpoch + 1);
        _contentSpaceEnvironment = next;
        _contentSpaceCandidate = null;
        _solvedDocument = null;
        spatial.NotifyContentEditorChanged(PosterId);
    }

    private ContentSpaceWitness? ContentWitness(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport, DesktopPixelRect frame)
    {
        if (_contentSpaceEnvironment is not { } environment || _authoritativePoster is null) return null;
        return new(snapshot.PosterId, _contentSurfaceSession, snapshot.DocumentVersion, snapshot.ItemSetVersion,
            _contentSpaceLayoutGeneration, environment, PosterSpatialRuntime.Rect(frame),
            StateRequestFingerprint.Compute(new { _presentation.Metrics, viewport.Scale, viewport.Spacing, _presentation.EffectiveContent }),
            viewport.ThemeMetricsVersion, viewport.Generation, viewport.WidthDip, viewport.HeightDip,
            _authoritativePoster.IsVisible, !_disposed && frame.IsUsable);
    }

    private ContentSpaceDecision? EvaluateContentSpace(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport,
        DesktopPixelRect frame, ContentBrowseAnchor? anchor)
    {
        // R4 owns the complete animation interval. Intermediate viewport clips and a collapsed
        // title are not physical shortages, nor are they new continuity/recovery publications.
        if (_transitionLayout is not null || _geometry.IsCollapsed || ContentWitness(snapshot, viewport, frame) is not { } witness)
            return null;
        var previous = _contentSpacePublication.Published;
        bool vertical = snapshot.FlowAxis == ContentFlowAxis.Vertical;
        var cause = ContentSpaceOwnerAdmission.Classify(witness, snapshot, previous,
            vertical ? _authoritativePoster!.WidthDip : _authoritativePoster!.ExpandedHeightDip);
        return ContentSpacePolicy.Evaluate(new(witness, snapshot, viewport, cause,
            AtSafePoint: !HasContentGesture && _posterMenu is null, Anchor: anchor), witness, previous);
    }

    private void PublishContentSpace(ContentSpaceDecision? candidate)
    {
        if (candidate is null || _contentEnvironmentPublishing || HasContentGesture || _transitionLayout is not null || _geometry.IsCollapsed ||
            _contentSnapshot is not { } snapshot || _contentViewport is not { } viewport ||
            _window is null || !HasExpectedBounds(_window, _bounds)) return;
        var old = _contentSpacePublication.Published;
        if (!_contentSpacePublication.TryPublish(candidate, ContentWitness(snapshot, viewport, _bounds), true)) return;
        if (old is null || old.IsRestricted != candidate!.IsRestricted || old.Witness.Environment != candidate.Witness.Environment)
        {
            log.Write("info", "content.space_published", new { PosterId, reason = candidate.Reason.ToString(),
                mode = candidate.BrowseMode.ToString(), candidate.Witness, candidate.Offsets, persisted = false });
            spatial.NotifyContentEditorChanged(PosterId);
        }
        if (old?.IsRestricted == true && !candidate!.IsRestricted) QueueContentProposalDrain();
    }

    private void BrowseRestrictedContent(int direction)
    {
        if (!IsContentSpaceRestricted || direction is < 0 or > 3 || _contentPresentationSuspended ||
            HasContentGesture || _transitionLayout is not null || _geometry.IsCollapsed) return;
        _contentActivation.Reset();
        var snapshot = EnsureContentSnapshot(_bounds);
        bool horizontal = direction < 2;
        int wheel = direction is 0 or 2 ? 120 : -120;
        var offsets = ContentWheelScrollPolicy.Advance(snapshot, _contentViewport!, wheel, _contentBrowseMode,
            crossAxis: horizontal == (snapshot.FlowAxis == ContentFlowAxis.Vertical));
        _contentScrollX = offsets.ScrollXDip; _itemScrollOffsetDip = offsets.ScrollYDip;
        RefreshContentSurface();
    }

    private ContentRect ContentNavigationRect(DesktopPixelRect frame, int direction)
    {
        double width = frame.Width / (_monitor?.ScaleX ?? 1) - ContentLeftDip - HorizontalPaddingDip - (_surface?.BorderThickness.Right ?? 0);
        return new(ContentLeftDip + direction * width / 4,
            frame.Height / (_monitor?.ScaleY ?? 1) - (_surface?.BorderThickness.Bottom ?? 0) - FooterHeightDip + 16, width / 4, 24);
    }

    private void AddContentSpaceNavigation(Grid structure)
    {
        var panel = _contentSpaceNavigation = new Grid { IsHitTestVisible = false, Visibility = Visibility.Collapsed,
            Margin = new Thickness(HorizontalPaddingDip, 0, HorizontalPaddingDip, 0), Background = ColorBrush("#17252F", 225) };
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        var hint = new TextBlock { Text = "空间受限 · 原布局保留", FontSize = 11, Foreground = ColorBrush("#FFFFFF"),
            HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        panel.Children.Add(hint);
        var arrows = new Grid { IsHitTestVisible = false };
        for (int i = 0; i < 4; i++)
        {
            arrows.ColumnDefinitions.Add(new ColumnDefinition());
            var text = new TextBlock { Text = new[] { "←", "→", "↑", "↓" }[i], FontSize = 17,
                Foreground = ColorBrush("#FFFFFF"), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            Grid.SetColumn(text, i); arrows.Children.Add(text);
        }
        Grid.SetRow(arrows, 1); panel.Children.Add(arrows);
        Grid.SetRow(panel, 3); structure.Children.Add(panel);
    }

    private void RenderContentSpaceNavigation()
    {
        if (_contentSpaceNavigation is not null) _contentSpaceNavigation.Visibility = IsContentSpaceRestricted &&
            !_disposed && !_contentPresentationSuspended && !_geometry.IsCollapsed && _transitionLayout is null
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddContentSpaceNavigationRegions(DesktopPixelRect frame, List<PosterInputRegion> regions)
    {
        if (!IsContentSpaceRestricted || _geometry.IsCollapsed || _monitor is null || _transitionLayout is not null) return;
        for (int i = 0; i < 4; i++)
        {
            var r = ContentNavigationRect(frame, i);
            int left = (int)Math.Round(r.X * _monitor.ScaleX), top = (int)Math.Round(r.Y * _monitor.ScaleY);
            int right = (int)Math.Round(r.Right * _monitor.ScaleX), bottom = (int)Math.Round(r.Bottom * _monitor.ScaleY);
            regions.Add(new(PosterInputTargetKind.ContentBrowseNavigation, i, new(left, top, right - left, bottom - top)));
        }
    }

    private void AddContentSpaceNavigationMenu(MenuFlyout flyout, Func<string, bool> current)
    {
        if (!IsContentSpaceRestricted) return;
        var menu = new MenuFlyoutSubItem { Text = "空间受限 · 浏览内容" };
        for (int i = 0; i < 4; i++)
        {
            int direction = i;
            var item = StyleMenuItem(new MenuFlyoutItem { Text = new[] { "向左", "向右", "向上", "向下" }[i] });
            item.Click += (_, _) => { if (current("browse-" + direction)) { flyout.Hide(); BrowseRestrictedContent(direction); } };
            menu.Items.Add(item);
        }
        flyout.Items.Add(menu); flyout.Items.Add(new MenuFlyoutSeparator());
    }
}
