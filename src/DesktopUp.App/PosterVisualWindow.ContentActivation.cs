using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private readonly ContentActivationSession _contentActivation = new();
    private long _contentOpenIntentCount;

    private ContentActivationTarget? ContentActivationTargetFor(string? id)
    {
        if (id is null || _disposed || _contentPresentationSuspended || _geometry.IsCollapsed ||
            _contentParseReadOnly || _contentProjectionFault || _transitionLayout is not null ||
            _monitor is null || _contentSnapshot is not { } snapshot || _contentViewport is not { } viewport ||
            !ContentDocument.Placements.Any(p => p.ItemId == id) ||
            _contentPorts.Views.Version != _contentSourceVersion ||
            ReadContentViews().FirstOrDefault(p => p.ItemId == id)?.IsAvailable != true) return null;
        // Selection paint and input-table revisions may change between clicks. Bind to actual
        // identity, author version, environment and presented geometry instead of paint churn.
        string witness = StateRequestFingerprint.Compute(new
        {
            inputSession, _posterEntityVersion, _contentSourceVersion, _bounds, _monitor.ScaleX, _monitor.ScaleY,
            viewport.WidthDip, viewport.HeightDip, viewport.ThemeMetricsVersion,
            snapshot.ScrollXDip, snapshot.ScrollYDip, snapshot.FlowAxis, snapshot.SpaceStatus,
            items = snapshot.Items.Select(p => new { p.ItemId, p.ReadOrder, p.Bounds }).ToArray()
        });
        return new(ContentDocument.PosterId, id, ContentDocument.ItemSetVersion,
            ContentDocument.DocumentVersion, witness);
    }

    private void ClickContent(ContentGesture gesture, PosterInputEventArgs args)
    {
        var target = ContentActivationTargetFor(gesture.Slot.ItemId);
        if (target is null || target != gesture.ActivationTarget)
        { _contentActivation.Reset(); return; }
        if (_contentActivation.Click(target, gesture.Capture.Id, Environment.TickCount64, args.LatestPoint,
            NativeContentInteraction.DoubleClickSettings) is { } intent) DispatchContentOpen(intent);
    }

    internal string? ActivateSelectedContent(bool isRepeat)
    {
        if (_contentPorts.UnavailableOpenReason is { } reason) return reason;
        if (HasContentGesture || spatial.IsPosterSpatialBusy(PosterId!) || PosterId is { } id && spatial.HasCenterDraft(id))
            return "请先完成或取消当前修改。";
        if (ContentActivationTargetFor(_selectedContentItem) is not { } target)
            return "请先选择可打开的项目。";
        if (_contentActivation.KeyDown(target, hasItemFocus: true, isRepeat) is { } intent)
            DispatchContentOpen(intent);
        return null;
    }

    internal void ReleaseContentActivationKey() => _contentActivation.KeyUp();

    private void DispatchContentOpen(ContentOpenIntent intent)
    {
        if (ContentActivationTargetFor(intent.Target.ItemId) != intent.Target) return;
        if (_contentPorts.UnavailableOpenReason is { } reason)
        { PublishContentFeedback(reason, transient: true); return; }
        if (_contentPorts.Open is IAsyncContentActions actions)
        { DispatchRealContentOpen(intent, actions); return; }
        try { _contentPorts.Open.Open(intent); }
        catch (Exception) { PublishContentFeedback("内容暂时无法打开，布局未改变。", transient: true); return; }
        // DEMO sink only. V3-05 supplies the real open adapter; layout commands never execute files.
        _contentOpenIntentCount++;
        log.Write("info", "poster.test_item_invoked", new
        { PosterId, itemId = intent.Target.ItemId, intent.ActivationId, origin = intent.Origin.ToString(), demo = true });
        var name = ReadContentViews().FirstOrDefault(p => p.ItemId == intent.Target.ItemId)?.Name;
        PublishContentFeedback($"已打开「{name ?? "所选项目"}」（演示）。", transient: true);
    }
}
