using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ContentFeedbackPhase { Preview, Committed }
public enum ContentFeedbackState { Preview, Committed, NoChange, Rejected, Cancelled }

/// <summary>Derived impact only. Categories may overlap; their counts must not be added together.
/// Offscreen means fully outside the frozen press snapshot's current finite viewport clip.</summary>
public sealed record ContentInteractionImpact
{
    public string? EditedItemId { get; init; }
    public bool AdoptsProjection { get; init; }
    public IReadOnlyList<string> AdoptedItemIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DisplacedItemIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OffscreenAffectedItemIds { get; init; } = Array.Empty<string>();
    public int? FinalPinnedPosition { get; init; }
    public bool EnteredManual { get; init; }
    public bool RestoredAutomatic { get; init; }
    public IReadOnlyList<string> RestoredPinnedItemIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RestoredSizeOverrideItemIds { get; init; } = Array.Empty<string>();
}

public sealed record ContentInteractionFeedback(ContentFeedbackState State, string Message, ContentInteractionImpact Impact)
{
    /// <summary>The original rejection/cancellation reason; adapters may explain it without losing diagnostic detail.</summary>
    public string? Reason { get; init; }
}

/// <summary>Presentation metadata from one existing editor result and its full press composition.
/// This class never solves, writes authoring, confirms a commit, or infers persistence from a preview.
/// R5 consumes finite clips; loop-copy visibility needs the later seam presentation contract.</summary>
public static class ContentInteractionFeedbackPolicy
{
    public static ContentInteractionFeedback Describe(ContentLayoutDocument beforeAuthor,
        ContentLayoutSnapshot pressPresentedSnapshot, ContentLayoutEdit edit, ContentLayoutEditResult result,
        ContentFeedbackPhase phase = ContentFeedbackPhase.Preview)
    {
        ArgumentNullException.ThrowIfNull(beforeAuthor);
        ArgumentNullException.ThrowIfNull(pressPresentedSnapshot);
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(result);
        if(!Enum.IsDefined(phase) || !Enum.IsDefined(edit.Kind))
            throw new ArgumentException("A known feedback phase and edit kind are required.");
        if(!result.Accepted) return Rejected(result.Error);
        RequireSameMembers(beforeAuthor,pressPresentedSnapshot,result.Document);
        if(ContentLayoutEditing.SameState(beforeAuthor,result.Document,ignoreVersions:true))
            return new(ContentFeedbackState.NoChange,NoChangeMessage(edit,phase),new());

        var before=beforeAuthor.Placements.ToDictionary(p=>p.ItemId,StringComparer.Ordinal);
        var after=result.Document.Placements.ToDictionary(p=>p.ItemId,StringComparer.Ordinal);
        var slots=pressPresentedSnapshot.Items.OrderBy(p=>p.ReadOrder).ThenBy(p=>p.ItemId,StringComparer.Ordinal).ToArray();
        var adopted=new HashSet<string>(StringComparer.Ordinal);
        var displaced=new HashSet<string>(StringComparer.Ordinal);
        bool manualGeometry=beforeAuthor.Mode==ContentLayoutMode.Manual &&
            edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize;
        if(result.AdoptsProjection)
        {
            // Include every logical member, not just visible cards. Saving an adapted coordinate
            // changes its future wide-layout restoration even when its current pixel does not move.
            foreach(var slot in slots)
            {
                var author=before[slot.ItemId];
                if(author.Column!=slot.Column-slot.RegionColumn || author.Row!=slot.Row-slot.RegionRow ||
                    author.ColumnSpan!=slot.ColumnSpan || author.RowSpan!=slot.RowSpan)
                    adopted.Add(slot.ItemId);
            }
        }
        if(manualGeometry)
        {
            var preview=result.PreviewSnapshot ?? throw new ArgumentException("A changed manual edit requires its existing full preview composition.");
            RequireSameMembers(result.Document,preview,result.Document);
            var finalSlots=preview.Items.ToDictionary(p=>p.ItemId,StringComparer.Ordinal);
            foreach(var slot in slots)
                if(slot.ItemId!=edit.ItemId && !SameGeometry(slot,finalSlots[slot.ItemId]))
                    displaced.Add(slot.ItemId);
        }
        bool entered=beforeAuthor.Mode!=ContentLayoutMode.Manual && result.Document.Mode==ContentLayoutMode.Manual;
        bool restored=beforeAuthor.Mode==ContentLayoutMode.Manual && result.Document.Mode!=ContentLayoutMode.Manual;
        var affected=new HashSet<string>(adopted,StringComparer.Ordinal);
        affected.UnionWith(displaced);
        string? edited=edit.ItemId is { } id && before.ContainsKey(id) ? id : null;
        if(edited is not null) affected.Add(edited);
        IReadOnlyList<string> Ordered(Func<string,bool> predicate)=>Array.AsReadOnly(slots.Where(p=>predicate(p.ItemId)).Select(p=>p.ItemId).ToArray());
        var impact=new ContentInteractionImpact
        {
            EditedItemId=edited, AdoptsProjection=result.AdoptsProjection,
            AdoptedItemIds=Ordered(adopted.Contains), DisplacedItemIds=Ordered(displaced.Contains),
            OffscreenAffectedItemIds=Array.AsReadOnly(slots.Where(p=>affected.Contains(p.ItemId) && p.Clip.IsEmpty).Select(p=>p.ItemId).ToArray()),
            FinalPinnedPosition=edited is not null && result.Document.Mode!=ContentLayoutMode.Manual ? after[edited].PinnedIndex : null,
            EnteredManual=entered, RestoredAutomatic=restored,
            RestoredPinnedItemIds=restored ? Ordered(memberId=>after[memberId].PinnedIndex.HasValue) : Array.Empty<string>(),
            RestoredSizeOverrideItemIds=restored ? Ordered(memberId=>after[memberId].ManualSize) : Array.Empty<string>()
        };
        return new(phase==ContentFeedbackPhase.Committed ? ContentFeedbackState.Committed : ContentFeedbackState.Preview,
            Message(beforeAuthor,edit,impact,phase),impact);
    }

    public static ContentInteractionFeedback Rejected(string? reason) => new(ContentFeedbackState.Rejected,
        string.IsNullOrWhiteSpace(reason) ? "本次操作未保存。" : reason+"；本次操作未保存。",new()) { Reason=reason };

    public static ContentInteractionFeedback Cancelled(string? reason = null) => new(ContentFeedbackState.Cancelled,
        "已取消本次修改，未保存。",new()) { Reason=reason };

    private static string NoChangeMessage(ContentLayoutEdit edit,ContentFeedbackPhase phase)
    {
        string subject=edit.Kind switch
        {
            ContentLayoutEditKind.Move=>"位置", ContentLayoutEditKind.Resize=>"尺寸",
            ContentLayoutEditKind.Mode=>"排布模式", _=>"设置"
        };
        return subject+"未改变，"+(phase==ContentFeedbackPhase.Preview ? "松手不保存。" : "未保存本次操作。");
    }

    private static string Message(ContentLayoutDocument before,ContentLayoutEdit edit,ContentInteractionImpact impact,ContentFeedbackPhase phase)
    {
        bool committed=phase==ContentFeedbackPhase.Committed;
        string action=committed ? "已" : "将";
        if(impact.EnteredManual)
            return action+"按当前整张排列进入手动；原自动图钉和尺寸设置"+(committed ? "已" : "将")+"保留，可一次撤销。";
        if(impact.RestoredAutomatic)
            return action+$"恢复原自动排布（{impact.RestoredPinnedItemIds.Count} 个顺序图钉、{impact.RestoredSizeOverrideItemIds.Count} 项尺寸覆盖）；手动位置和大小不带回，可一次撤销。";
        if(edit.Kind==ContentLayoutEditKind.Move && before.Mode!=ContentLayoutMode.Manual &&
            before.Placements.Single(p=>p.ItemId==edit.ItemId).FixedPosition==false && impact.FinalPinnedPosition is { } pin)
            return (committed ? "已固定" : "松手固定")+$"到第 {pin} 位；其他图钉保留，未固定项仍自动排序，可一次撤销。";
        if(edit.Kind==ContentLayoutEditKind.Pinned)
            return impact.FinalPinnedPosition is { } fixedPin
                ? action+$"固定到第 {fixedPin} 位；其他图钉和尺寸设置保留，可一次撤销。"
                : action+"解除顺序图钉，所选项重新跟随自动排序；尺寸设置保留，可一次撤销。";
        if(edit.Kind==ContentLayoutEditKind.SizeOverride)
            return action+(edit.Enabled==true ? "固定当前显示尺寸" : "恢复原自动尺寸设置")+"；顺序图钉不变，可一次撤销。";
        if(edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize)
        {
            string operation=edit.Kind==ContentLayoutEditKind.Move ? "移动所选项" : "调整所选项尺寸";
            string start=committed ? "已" : "松手";
            string message=impact.AdoptsProjection
                ? start+$"采用当前整张构图（{impact.AdoptedItemIds.Count} 项适配位置"+(committed ? "已" : "将")+"保留）并"+operation
                : start+operation;
            if(impact.DisplacedItemIds.Count>0) message+=$"；{impact.DisplacedItemIds.Count} 项让位";
            if(impact.OffscreenAffectedItemIds.Count>0) message+=$"；涉及 {impact.OffscreenAffectedItemIds.Count} 项屏外内容";
            return message+"，可一次撤销。";
        }
        return action+"更新辅助显示，可一次撤销。";
    }

    private static bool SameGeometry(ContentResolvedPlacement a,ContentResolvedPlacement b) =>
        a.Column==b.Column && a.Row==b.Row && a.ColumnSpan==b.ColumnSpan && a.RowSpan==b.RowSpan &&
        a.RegionColumn==b.RegionColumn && a.RegionRow==b.RegionRow;

    private static void RequireSameMembers(ContentLayoutDocument author,ContentLayoutSnapshot snapshot,ContentLayoutDocument after)
    {
        var ids=author.Placements.Select(p=>p.ItemId).ToHashSet(StringComparer.Ordinal);
        if(author.PosterId!=snapshot.PosterId || author.DocumentVersion!=snapshot.DocumentVersion ||
            author.ItemSetVersion!=snapshot.ItemSetVersion || after.PosterId!=author.PosterId ||
            snapshot.Items.Count!=ids.Count || snapshot.Items.Select(p=>p.ItemId).Distinct(StringComparer.Ordinal).Count()!=ids.Count ||
            after.Placements.Count!=ids.Count || !ids.SetEquals(snapshot.Items.Select(p=>p.ItemId)) ||
            !ids.SetEquals(after.Placements.Select(p=>p.ItemId)))
            throw new ArgumentException("Feedback requires the complete matching author, presented member set and edit result.");
    }
}
