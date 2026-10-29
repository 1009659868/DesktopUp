namespace DesktopUp.Domain;

/// <summary>Bounded immutable history. A gesture submits once, not once per pointer sample.</summary>
public sealed class CanvasAuthoring
{
    private readonly List<CanvasDesign> undo = [], redo = [];
    public CanvasDesign Design { get; private set; }
    public bool CanUndo => undo.Count != 0;
    public bool CanRedo => redo.Count != 0;
    public CanvasAuthoring(CanvasDesign design) => Design = CanvasDeclaration.Freeze(design);
    public void Set(CanvasDesign next)
    {
        next = CanvasDeclaration.Freeze(next);
        if (CanvasDeclaration.Digest(next) == CanvasDeclaration.Digest(Design)) return;
        undo.Add(Design); if (undo.Count > 40) undo.RemoveAt(0);
        redo.Clear(); Design = next;
    }
    public void Undo() { if (!CanUndo) return; redo.Add(Design); Design = undo[^1]; undo.RemoveAt(undo.Count - 1); }
    public void Redo() { if (!CanRedo) return; undo.Add(Design); Design = redo[^1]; redo.RemoveAt(redo.Count - 1); }
    public void Replace(CanvasNode node) => Set(WithNodes(Design.Recipe.Nodes.Select(n => n.Id == node.Id ? node : n)));
    public CanvasDesign WithNodes(IEnumerable<CanvasNode> nodes) => Design with { Recipe = Design.Recipe with { Nodes = nodes.ToArray(), ProtocolVersion = Math.Max(2, Design.Recipe.ProtocolVersion) } };
    public static string NewId() => "n-" + Guid.NewGuid().ToString("N");
    public static CanvasDesign Blank(string name = "空白创作") => CanvasDeclaration.Freeze(new(
        new(NewId(), 1, name, [new("root", null, "layout.canvas@1", new(), new Dictionary<string, CanvasValue>()),
            new(NewId(), "root", "content.slot@1", new(.07, .12, .86, .8), new Dictionary<string, CanvasValue>(), Name: "内容区")], [], ProtocolVersion: 2),
        new Dictionary<string, CanvasValue>()));
    public static CanvasDesign NewArtwork(string name = "空白创作")
    { var blank = Blank(name); return CanvasSurfaceGeometry.ForEditing(blank, 720, 540); }
    public IReadOnlyList<CanvasNode> Subtree(IEnumerable<string> selected)
    {
        var keep = selected.ToHashSet(StringComparer.Ordinal);
        foreach (var n in Design.Recipe.Nodes) if (n.Parent is not null && keep.Contains(n.Parent)) keep.Add(n.Id);
        return Design.Recipe.Nodes.Where(n => keep.Contains(n.Id)).ToArray();
    }
    public void Delete(IEnumerable<string> selected)
    {
        var remove = Subtree(selected);
        if (remove.Any(n => IsLocked(n.Id))) throw new ArgumentException("请先解锁对象再删除。");
        if (remove.Any(n => n.Parent is null || n.Type is "content.slot@1" or "host.title@1")) throw new ArgumentException("保留根节点、标题操作区和唯一内容区。");
        var ids = remove.Select(n => n.Id).ToHashSet();
        var next = WithNodes(Design.Recipe.Nodes.Where(n => !ids.Contains(n.Id)));
        if (next.Recipe.Behavior is { } behavior) next = next with { Recipe = next.Recipe with
            { Behavior = behavior with { Rules = behavior.Rules?.Where(r => !ids.Contains(r.Source) && !ids.Contains(r.Target)).ToArray(),
                Bindings = behavior.Bindings?.Where(b => !ids.Contains(b.Source) && !ids.Contains(b.Target)).ToArray() } } };
        Set(next);
    }
    public string Group(IEnumerable<string> selected)
    {
        var ids = selected.ToHashSet();
        var top = Design.Recipe.Nodes.Where(n => ids.Contains(n.Id) && (n.Parent is null || !ids.Contains(n.Parent))).ToArray();
        if (top.Length < 2 || top.Any(n => n.Parent is null) || top.Select(n => n.Parent).Distinct().Count() != 1)
            throw new ArgumentException("请选中同一层级的至少两个对象。");
        if (Subtree(ids).Any(n => n.Type is "content.slot@1" or "host.title@1")) throw new ArgumentException("组合只包含装饰，不复制标题操作区或真实内容区。");
        if (top.Any(n => IsLocked(n.Id))) throw new ArgumentException("请先解锁对象再组合。");
        double x = top.Min(n => n.Box.X), y = top.Min(n => n.Box.Y), w = top.Max(n => n.Box.X + n.Box.Width) - x, h = top.Max(n => n.Box.Y + n.Box.Height) - y;
        var group = new CanvasNode(NewId(), top[0].Parent, "layout.group@1", new(x, y, w, h), new Dictionary<string, CanvasValue>(), Name: "组合");
        var tree = Subtree(top.Select(n => n.Id)); var treeIds = tree.Select(n => n.Id).ToHashSet(); var tops = top.Select(n => n.Id).ToHashSet();
        var nodes = Design.Recipe.Nodes.Where(n => !treeIds.Contains(n.Id)).ToList();
        // Reinsert as one contiguous subtree, preserving the selected siblings' own order.
        int position = Design.Recipe.Nodes.TakeWhile(n => n.Id != top[0].Id).Count(n => !treeIds.Contains(n.Id));
        nodes.InsertRange(position, new[] { group }.Concat(tree.Select(n => tops.Contains(n.Id) ? n with
        { Parent = group.Id, Box = new(Math.Clamp((n.Box.X - x) / w, 0, 1), Math.Clamp((n.Box.Y - y) / h, 0, 1), Math.Min(1, n.Box.Width / w), Math.Min(1, n.Box.Height / h)) } : n)));
        Set(WithNodes(nodes)); return group.Id;
    }
    public bool IsLocked(string id) => CanvasLayerState.Resolve(Design).Locked.Contains(id);
    public IReadOnlyList<string> Ungroup(string id)
    {
        var group = Design.Recipe.Nodes.Single(n => n.Id == id);
        if (group.Type != "layout.group@1" || IsLocked(id)) throw new ArgumentException("请选择未锁定的组合。");
        if (Design.Recipe.Behavior?.Bindings?.Any(b => b.Source == id) == true)
            throw new ArgumentException("组合本身绑定了行为，请先移除该绑定再取消组合；子图层绑定会保留。");
        var children = Design.Recipe.Nodes.Where(n => n.Parent == id).Select(n => n.Id).ToArray();
        Set(WithNodes(Design.Recipe.Nodes.Where(n => n.Id != id).Select(n => n.Parent == id ? n with
        {
            Parent = group.Parent, Hidden = group.Hidden || n.Hidden,
            Box = UngroupBox(n, group), Rotation = (n.Rotation + group.Rotation + 540) % 360 - 180
        } : n)));
        return children;
    }
    private CanvasBox UngroupBox(CanvasNode child, CanvasNode group)
    {
        // Layout supplies the actual parent aspect; normalized coordinates are not a square.
        var art = Design.Recipe.Artboard ?? new CanvasArtboard(1, 1);
        var layout = CanvasLayout.ForAuthoring(Design, art.Width, art.Height);
        var parent = layout.Nodes.Single(p => p.Node.Id == group.Parent).Bounds;
        var placed = layout.Nodes.Single(p => p.Node.Id == child.Id).Bounds;
        var parentRotation = layout.Nodes.Single(p => p.Node.Id == group.Parent).Rotation;
        var center = CanvasEditingGeometry.Rotate(placed.X + placed.Width / 2 - parent.X - parent.Width / 2,
            placed.Y + placed.Height / 2 - parent.Y - parent.Height / 2, -parentRotation);
        return new((center.X + parent.Width / 2 - placed.Width / 2) / parent.Width,
            (center.Y + parent.Height / 2 - placed.Height / 2) / parent.Height, placed.Width / parent.Width, placed.Height / parent.Height);
    }
    public string Insert(IReadOnlyList<CanvasNode> source, string parent, double x = .12, double y = .12, CanvasBehavior? behavior = null)
    {
        if (source is null || source.Count == 0 || source.Any(n => n is null || n.Type is "content.slot@1" or "host.title@1" or "layout.canvas@1")) throw new ArgumentException("仅可复用装饰组合。");
        var map = source.ToDictionary(n => n.Id, _ => NewId());
        var roots = source.Where(n => n.Parent is null || !map.ContainsKey(n.Parent)).ToArray();
        if (roots.Length != 1) throw new ArgumentException("组合必须有单个根节点。");
        var root = roots[0];
        var inserted = source.Select(n => n with { Id = map[n.Id], Parent = n.Id == root.Id ? parent : map[n.Parent!],
            Box = n.Id == root.Id ? n.Box with { X = Math.Clamp(x, 0, 1 - n.Box.Width), Y = Math.Clamp(y, 0, 1 - n.Box.Height) } : n.Box }).ToArray();
        var next = WithNodes(Design.Recipe.Nodes.Concat(inserted));
        if (behavior is not null)
        {
            var existing = CanvasBehaviorPrefabs.ForEditing(Design);
            if (behavior.Prefabs is null || behavior.Bindings is null || behavior.Rules?.Count > 0)
                throw new ArgumentException("组合必须携带完整的预制体行为快照。");
            var prefabMap = (behavior.Prefabs ?? []).ToDictionary(p => p.Id, _ => NewId());
            if (behavior.Bindings.Any(b => !map.ContainsKey(b.Source) || b.Target != "poster" && !map.ContainsKey(b.Target) || !prefabMap.ContainsKey(b.Prefab)))
                throw new ArgumentException("组合行为不能引用组合外的图层或缺失的预制体。");
            var remapped = (behavior.Bindings ?? []).Select(b => b with { Id = NewId(), Source = map[b.Source],
                Target = b.Target == "poster" ? "poster" : map[b.Target], Prefab = prefabMap[b.Prefab] }).ToArray();
            next = next with { Recipe = next.Recipe with { Behavior = existing with {
                Prefabs = existing.Prefabs!.Concat(behavior.Prefabs!.Select(p => p with { Id = prefabMap[p.Id] })).ToArray(),
                Bindings = existing.Bindings!.Concat(remapped).ToArray() } } };
        }
        Set(next); return map[root.Id];
    }
    public CanvasBehavior? ExportBehavior(string id)
    {
        var ids = Subtree([id]).Select(n => n.Id).ToHashSet();
        var value = CanvasBehaviorPrefabs.ForEditing(Design);
        var bindings = value.Bindings!.Where(b => ids.Contains(b.Source)).ToArray();
        if (bindings.Length == 0) return null;
        if (bindings.Any(b => b.Target != "poster" && !ids.Contains(b.Target)))
            throw new ArgumentException("组合行为指向组合外的图层，请先将目标一起组合或移除该绑定。");
        var prefabs = bindings.Select(b => b.Prefab).ToHashSet();
        return new(Prefabs: value.Prefabs!.Where(p => prefabs.Contains(p.Id)).ToArray(), Bindings: bindings);
    }
    public IReadOnlyList<CanvasNode> Export(string id)
    {
        var nodes = Subtree([id]);
        if (nodes.Count == 0 || nodes.Any(n => n.Type is "content.slot@1" or "host.title@1" or "layout.canvas@1")) throw new ArgumentException("请选中装饰或装饰组合。");
        return nodes.Select(n => n with { Parent = n.Id == id ? null : n.Parent, Properties = n.Properties.ToDictionary(p => p.Key, p => CanvasDeclaration.Resolve(Design, p.Value)) }).ToArray();
    }
}
