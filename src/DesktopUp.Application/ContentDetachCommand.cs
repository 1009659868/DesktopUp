using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentDetachCommand(string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, string PosterId, IReadOnlyList<string> ItemIds, bool Confirmed);

public static class ContentDetachPolicy
{
    public static StateMutationPlan Prepare(ContentDetachCommand command, DesktopStateSnapshot state)
    {
        StateMutationPlan Reject(string message) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, message);
        if (command.Source is not (StateCommandSource.Poster or StateCommandSource.ControlCenter)) return Reject("Invalid content surface.");
        if(!command.Confirmed) return StateMutationPlan.Reject(StateCommandStatus.ConfirmationRequired,"仅从此海报移除引用，原文件与其他海报不变。请确认。");
        if(command.ReadSet.ContentVersion!=state.ContentVersion || !command.ReadSet.Posters.ContainsKey(command.PosterId)) return Reject("Missing content/poster read set.");
        var poster=state.Catalog.Posters.SingleOrDefault(p=>p.Poster.PosterId==command.PosterId)?.Poster;
        if(poster is null) return StateMutationPlan.Reject(StateCommandStatus.NotFound,"海报已不存在。");
        if(poster.IsLocked) return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied,"海报已锁定，请先解锁。");
        var ids=command.ItemIds.ToHashSet(StringComparer.Ordinal);
        if(ids.Count is <1 or >1000 || ids.Count!=command.ItemIds.Count) return Reject("Invalid item selection.");
        var removed=state.ContentCatalog.PosterItems.Where(p=>p.PosterId==command.PosterId && ids.Contains(p.LayoutItemId)).ToArray();
        if(removed.Length!=ids.Count) return Reject("所选关联已变化，请重新选择。");
        if(!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson,poster.PosterId,out var layout,out var error)) return Reject(error!);
        // Positions/spans stay authored. Only anchors beyond the new cardinality are compacted
        // from the tail, preserving pin order, including dormant automatic return intent.
        var survivors=layout!.Placements.Where(p=>!ids.Contains(p.ItemId)).ToArray();
        Dictionary<string,int> Pins(Func<ContentPlacement,int?> select)
        {
            int ceiling=survivors.Length; var result=new Dictionary<string,int>();
            foreach(var p in survivors.Where(p=>select(p).HasValue).OrderByDescending(p=>select(p)))
            { int value=Math.Min(select(p)!.Value,ceiling);result[p.ItemId]=value;ceiling=value-1; }
            return result;
        }
        var pins=Pins(p=>p.PinnedIndex); var savedPins=Pins(p=>p.SavedAutomaticState?.PinnedIndex);
        survivors=survivors.Select(p=>
        {
            var updated=p with { PinnedIndex=pins.TryGetValue(p.ItemId,out int pin)?pin:null,
                SavedAutomaticState=p.SavedAutomaticState is { } saved ? saved with { PinnedIndex=savedPins.TryGetValue(p.ItemId,out int oldPin)?oldPin:null } : null };
            return updated==p?p:updated with { Version=checked(p.Version+1) };
        }).ToArray();
        var next=layout! with { Placements=survivors,
            DocumentVersion=checked(layout.DocumentVersion+1),ItemSetVersion=checked(layout.ItemSetVersion+1) };
        ContentLayoutCommandPolicy.ValidateAuthoring(next);
        return new() { ContentCatalog=state.ContentCatalog with { PosterItems=state.ContentCatalog.PosterItems.Except(removed).ToArray() },
            ExplicitDetachedRelations=removed.Select(p=>p.Id).ToArray(),
            Updates=[poster with {ContentLayoutJson=ContentLayoutPolicy.Serialize(next)}],ResultPosterId=poster.PosterId };
    }
}
public sealed partial class StateProjectionCoordinator
{
    public StateCommandResult Execute(ContentDetachCommand command)=>Run(command.RequestId,command.Source,command.ExpectedRevision,command.ReadSet,
        reads=>command with { ReadSet=Required(reads),ItemIds=Array.AsReadOnly(command.ItemIds.ToArray()) },ContentDetachPolicy.Prepare);
}
