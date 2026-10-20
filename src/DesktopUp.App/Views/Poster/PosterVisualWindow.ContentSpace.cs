using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private readonly Guid _contentSurfaceSession = Guid.NewGuid();
    private SpatialEnvironment? _contentSpaceEnvironment;
    private long _contentSpaceLayoutGeneration, _contentSpaceEpoch;
    private bool _contentEnvironmentPublishing;
    private bool IsContentSpaceRestricted => _contentLayout.BrowseMode == ContentBrowseMode.ReadOnlyOverflow;
    private Grid? _contentSpaceNavigation;

    private void SetContentEnvironment(DisplayMonitorSnapshot monitor, long display, long shell)
    {
        var next = spatial.ContentEnvironment(monitor, display, shell);
        if (_contentSpaceEnvironment == next) return;
        if (_contentLayout.Snapshot is not null && _contentLayout.Viewport is not null && !_contentLayout.HasFault)
            _contentLayout.EndAnchor = ContentLayoutFlow.CaptureAnchor(_contentLayout.Snapshot, _contentLayout.Viewport, _contentLayout.BrowseMode);
        CancelContentGesture("physical-environment-changed");
        _contentActivation.Reset();
        _contentSpaceEpoch = checked(_contentSpaceEpoch + 1);
        _contentSpaceEnvironment = next;
        _contentLayout.Invalidate();
        spatial.NotifyContentEditorChanged(PosterId);
    }

    private ResolvedPosterPresentation? _contentMetricsSource;
    private string _contentMetricsFingerprint = "";
    private ContentLayoutSurface? ContentSurface(DesktopPixelRect frame)
    {
        if (_contentSpaceEnvironment is not { } environment || _authoritativePoster is not { } poster) return null;
        if (!ReferenceEquals(_contentMetricsSource, _presentation))
        {
            var preferences = _presentation.Parameters.Preferences ?? new();
            _contentMetricsFingerprint = StateRequestFingerprint.Compute(new
            { _presentation.Metrics, preferences.Scale, preferences.Spacing, _presentation.EffectiveContent });
            _contentMetricsSource = _presentation;
        }
        return new(_contentSurfaceSession, _contentSpaceLayoutGeneration, environment, PosterSpatialRuntime.Rect(frame),
            _contentMetricsFingerprint, poster.IsVisible, !_disposed && frame.IsUsable, poster.WidthDip, poster.ExpandedHeightDip);
    }

    private ContentSpaceWitness? ContentWitness(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport, DesktopPixelRect frame) =>
        ContentSurface(frame)?.Witness(snapshot, viewport);

    private void PublishContentSpace(ContentSpaceDecision? candidate)
    {
        if (candidate is null || _contentEnvironmentPublishing || HasContentGesture || _transitionLayout is not null || _geometry.IsCollapsed ||
            _contentLayout.Snapshot is not { } snapshot || _contentLayout.Viewport is not { } viewport ||
            _window is null || !HasExpectedBounds(_window, _bounds)) return;
        var old = _contentLayout.Space.Published;
        if (!_contentLayout.Space.TryPublish(candidate, ContentWitness(snapshot, viewport, _bounds), true)) return;
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
        if (!IsContentSpaceRestricted || direction is < 0 or > 3 || _contentSession.IsSuspended ||
            HasContentGesture || _transitionLayout is not null || _geometry.IsCollapsed) return;
        _contentActivation.Reset();
        var snapshot = EnsureContentSnapshot(_bounds);
        bool horizontal = direction < 2;
        int wheel = direction is 0 or 2 ? 120 : -120;
        var offsets = ContentWheelScrollPolicy.Advance(snapshot, _contentLayout.Viewport!, wheel, _contentLayout.BrowseMode,
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
            !_disposed && !_contentSession.IsSuspended && !_geometry.IsCollapsed && _transitionLayout is null
            ? Visibility.Visible : Visibility.Collapsed;
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
