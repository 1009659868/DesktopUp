using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ContentReferenceChoice { Skip, CreateIndependent, Reuse }
public sealed record ContentReferenceEntry(ContentItem Item, ContentReferenceChoice Choice, Guid? ReuseItemId = null);
public sealed record ContentReferenceCommand(string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, string PosterId, IReadOnlyList<ContentReferenceEntry> Entries,
    bool ConfirmDemoReplacement = false, Guid? RepairContentId = null);

public static class ContentReferencePolicy
{
    public const int MaximumBatch = 100;

    public static IReadOnlyList<string> AffectedPosters(ContentCatalogSnapshot catalog, Guid itemId)
    {
        var members = catalog.Memberships.Where(m => m.ContentItemId == itemId).Select(m => m.Id).ToHashSet();
        return catalog.PosterItems.Where(p => members.Contains(p.MembershipId)).Select(p => p.PosterId)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static StateMutationPlan Prepare(ContentReferenceCommand command, DesktopStateSnapshot state)
    {
        if (command.Source != StateCommandSource.ControlCenter) return Invalid("Reference editing is only available from the control center.");
        if (command.ReadSet.ContentVersion != state.ContentVersion || !command.ReadSet.Posters.ContainsKey(command.PosterId))
            return Invalid("The confirmed content and poster versions are required.");
        var target = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (target is null) return StateMutationPlan.Reject(StateCommandStatus.NotFound, "海报已不存在。");
        if (command.Entries.Count is < 1 or > MaximumBatch) return Invalid("一次最多处理 100 项。");
        foreach (var entry in command.Entries)
        {
            ContentCatalogPolicy.ValidateItem(entry.Item);
            if (!Enum.IsDefined(entry.Choice)) return Invalid("Invalid reference choice.");
        }
        if (command.RepairContentId is { } repairId) return Repair(command, state, repairId);
        var selected = command.Entries.Where(e => e.Choice != ContentReferenceChoice.Skip).ToArray();
        if (selected.Length == 0) return new() { ResultPosterId = target.PosterId };
        if (target.IsLocked) return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "海报已锁定，请解锁后添加内容。");
        if (selected.Any(e => !ContentCapabilityPolicy.Evaluate(e.Item, ContentOperation.Reference).Allowed))
            return Invalid("所选类型或来源不支持持久引用。");
        if (!ContentLayoutPolicy.TryRead(target.ContentLayoutJson, target.PosterId, out var layout, out var error))
            return Invalid(error!);
        var catalog = state.ContentCatalog;
        var binding = catalog.Bindings.SingleOrDefault(b => b.PosterId == target.PosterId);
        if (binding is null && (!command.ConfirmDemoReplacement || target.CollectionId is not null))
            return StateMutationPlan.Reject(StateCommandStatus.ConfirmationRequired,
                "首次添加需要确认替换 DEMO 构图；旧构图归档，原文件不动。既有未知集合不能自动转换。");
        if (binding is not null && catalog.Collections.Single(c => c.Id == binding.CollectionId).Kind != ContentCollectionKind.Reference)
            return Invalid("Only explicit reference collections accept additions.");
        var items = catalog.Items.ToList(); var collections = catalog.Collections.ToList();
        var members = catalog.Memberships.ToList(); var bindings = catalog.Bindings.ToList();
        var relations = catalog.PosterItems.ToList();
        if (binding is null)
        {
            var collection = new Collection(Guid.NewGuid(), target.Title.Length == 0 ? "引用内容" : target.Title, ContentCollectionKind.Reference);
            collections.Add(collection); binding = new(target.PosterId, collection.Id); bindings.Add(binding);
            layout = layout! with { Placements = [], Mode = ContentLayoutMode.Automatic, SavedAutomaticMode = null,
                Regions = [new("main", 0)], ReservedSpaces = [] };
        }
        var placements = layout!.Placements.ToList();
        foreach (var entry in selected)
        {
            ContentItem item;
            if (entry.Choice == ContentReferenceChoice.Reuse)
            {
                item = items.SingleOrDefault(i => i.Id == entry.ReuseItemId)!;
                if (item is null || ContentIdentityPolicy.Compare(item, entry.Item) == ContentDuplicateHint.None)
                    return Invalid("复用对象已不存在或来源不匹配，请重新预览。");
                if (!ContentCapabilityPolicy.Evaluate(item, ContentOperation.Reference).Allowed) return Invalid("复用对象不支持引用。");
            }
            else
            {
                if (entry.ReuseItemId is not null || entry.Item.Version != 1 || items.Any(i => i.Id == entry.Item.Id) ||
                    entry.Item.Availability != ContentAvailability.Available || !entry.Item.Capabilities.HasFlag(ContentCapability.Reference))
                    return Invalid("新增项必须是已确认可引用的独立实体。");
                item = entry.Item; items.Add(item);
            }
            var member = members.SingleOrDefault(m => m.CollectionId == binding.CollectionId && m.ContentItemId == item.Id);
            if (member is null) { member = new(Guid.NewGuid(), binding.CollectionId, item.Id); members.Add(member); }
            if (relations.Any(r => r.PosterId == target.PosterId && r.MembershipId == member.Id)) continue;
            var relation = new PosterItem(Guid.NewGuid(), target.PosterId, member.Id); relations.Add(relation);
            int order = placements.Count == 0 ? 0 : checked(placements.Max(p => p.Order) + 1);
            var placement = new ContentPlacement(relation.LayoutItemId, order, Math.Min(4, layout.MaximumColumns), 4)
                { RegionId = layout.Regions.OrderBy(r => r.Order).First().Id };
            if (layout.Mode == ContentLayoutMode.Manual)
            {
                // Append after authored content/reservations, never push or overwrite the old composition.
                bool vertical = layout.FlowAxis == ContentFlowAxis.Vertical;
                int end = placements.Select(p => vertical ? (p.Row ?? 0) + p.RowSpan : (p.Column ?? 0) + p.ColumnSpan)
                    .Concat(layout.ReservedSpaces.Select(s => vertical ? s.Row + s.RowSpan : s.Column + s.ColumnSpan)).DefaultIfEmpty(0).Max();
                placement = placement with { Column = vertical ? 0 : end, Row = vertical ? end : 0,
                    SavedAutomaticState = layout.SavedAutomaticMode is null ? null : new(order, placement.ColumnSpan, placement.RowSpan, false, null) };
            }
            placements.Add(placement);
        }
        if (relations.Count == catalog.PosterItems.Count) return new() { ResultPosterId = target.PosterId };
        layout = layout with { Placements = placements, DocumentVersion = checked(layout.DocumentVersion + 1),
            ItemSetVersion = checked(layout.ItemSetVersion + 1) };
        var preferences = PosterPresentationPolicy.Resolve(target).Parameters.Preferences ?? new();
        ContentLayoutCommandPolicy.ValidateAuthoring(layout, preferences.Scale, preferences.Spacing);
        return new() { ContentCatalog = ContentCatalogPolicy.Freeze(new(items, collections, members, bindings, relations)),
            Updates = [target with { CollectionId = binding.CollectionId.ToString(), ContentLayoutJson = ContentLayoutPolicy.Serialize(layout) }],
            ResultPosterId = target.PosterId };
    }

    private static StateMutationPlan Repair(ContentReferenceCommand command, DesktopStateSnapshot state, Guid id)
    {
        var current = state.ContentCatalog.Items.SingleOrDefault(i => i.Id == id);
        var affected = AffectedPosters(state.ContentCatalog, id);
        if (current is null || !affected.Contains(command.PosterId) || affected.Any(p => !command.ReadSet.Posters.ContainsKey(p)))
            return Invalid("修复必须确认全部受影响海报，不能只更新其中一张。");
        if (command.Entries.Count != 1 || command.Entries[0] is not { Choice: ContentReferenceChoice.CreateIndependent, ReuseItemId: null } entry ||
            entry.Item.Availability != ContentAvailability.Available || !ContentCapabilityPolicy.Evaluate(entry.Item, ContentOperation.Reference).Allowed)
            return Invalid("请选择一个可用来源进行共享修复。");
        var nextItem = entry.Item with { Id = current.Id, Version = checked(current.Version + 1) };
        return new() { ContentCatalog = ContentCatalogPolicy.Freeze(state.ContentCatalog with
            { Items = state.ContentCatalog.Items.Select(i => i.Id == id ? nextItem : i).ToArray() }),
            Updates = state.Catalog.Posters.Where(p => affected.Contains(p.Poster.PosterId)).Select(p => p.Poster).ToArray(),
            ResultPosterId = command.PosterId };
    }

    private static StateMutationPlan Invalid(string reason) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, reason);
}

public sealed partial class StateProjectionCoordinator
{
    public StateCommandResult Execute(ContentReferenceCommand command) => Run(command.RequestId, command.Source,
        command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = Required(reads), Entries = Array.AsReadOnly(command.Entries.ToArray()) },
        ContentReferencePolicy.Prepare);
}
