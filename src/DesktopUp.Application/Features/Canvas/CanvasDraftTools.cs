using DesktopUp.Domain;
using System.Security.Cryptography;
using System.Text;

namespace DesktopUp.Application;

public sealed record CanvasDraftOperation(string Kind, CanvasNode? Node = null, string? Target = null, CanvasBehavior? Behavior = null);
public sealed record CanvasDraftBatch(string RequestId, string Scope, string CapabilityRevision, long TargetVersion,
    long DraftRevision, string Digest, IReadOnlyList<CanvasDraftOperation> Operations);
public sealed record CanvasDraftReceipt(string RequestId, long DraftRevision, string Digest);

/// <summary>UI-thread scoped in-process design tools. Intentionally no Apply, filesystem or network verb.</summary>
public sealed class CanvasDraftTools(CanvasDesignSession session)
{
    private readonly Dictionary<string, (string Hash, CanvasDraftReceipt Receipt)> receipts = new(StringComparer.Ordinal);
    public string Scope { get; } = Guid.NewGuid().ToString("N");
    public CanvasScenePacket Read() => session.DescribeScene(draft: true);
    public CanvasDraftReceipt Execute(CanvasDraftBatch request)
    {
        if (request is null || request.RequestId is not { Length: > 0 and <= 64 } || request.Scope != Scope ||
            request.CapabilityRevision != CanvasCapabilities.Revision || request.Operations is not { Count: > 0 and <= 32 })
            throw new ArgumentException("草稿请求范围、能力版本或预算不合法。");
        string payload = CanvasDeclaration.Serialize(request);
        if (Encoding.UTF8.GetByteCount(payload) > 64 * 1024) throw new ArgumentException("单次草稿操作超过64 KiB预算。");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var before = Read(); // Fresh target check before every read, replay and write.
        if (receipts.TryGetValue(request.RequestId, out var saved))
        {
            if (saved.Hash != hash || saved.Receipt.DraftRevision != before.DraftRevision || saved.Receipt.Digest != before.DesignDigest)
                throw new ArgumentException("请求重放冲突或草稿已变化；未重复操作。");
            return saved.Receipt;
        }
        if (request.TargetVersion != before.TargetVersion || request.DraftRevision != before.DraftRevision || request.Digest != before.DesignDigest)
            throw new ArgumentException("草稿／目标版本或摘要已变化，请重新读取。");
        if (receipts.Count == 128) throw new ArgumentException("本工具会话已达128次操作，请由宿主重新建立会话。");
        var draft = new CanvasAuthoring(before.Design);
        foreach (var operation in request.Operations)
        {
            if (operation is null) throw new ArgumentException("缺少草稿操作。");
            switch (operation.Kind)
            {
                case "add" when operation is { Node: { } node, Target: null, Behavior: null }:
                    if (!CanvasBehaviorPolicy.IsDecoration(node) || draft.Design.Recipe.Nodes.Any(n => n.Id == node.Id) ||
                        node.Parent is null || draft.IsLocked(node.Parent)) throw new ArgumentException("仅可新增未锁定层级的装饰对象。");
                    draft.Set(draft.WithNodes(draft.Design.Recipe.Nodes.Append(node))); break;
                case "replace" when operation is { Node: { } node, Target: null, Behavior: null }:
                    var old = draft.Design.Recipe.Nodes.SingleOrDefault(n => n.Id == node.Id) ?? throw new ArgumentException("节点不存在。");
                    if (old.Parent is null || old.Parent != node.Parent || old.Type != node.Type || old.Locked != node.Locked || draft.IsLocked(old.Id))
                        throw new ArgumentException("不能替换节点身份、类型、父级或编辑锁。");
                    draft.Replace(node); break;
                case "remove" when operation is { Target: { } id, Node: null, Behavior: null }:
                    if (!draft.Design.Recipe.Nodes.Any(n => n.Id == id)) throw new ArgumentException("节点不存在。");
                    draft.Delete([id]); break;
                case "behavior" when operation is { Node: null, Target: null }:
                    CanvasBehaviorPrefabs.RequireUnlockedEdit(draft.Design, operation.Behavior);
                    draft.Set(draft.Design with { Recipe = draft.Design.Recipe with { Behavior = operation.Behavior } }); break;
                default: throw new ArgumentException("未知或含多余参数的草稿操作。");
            }
        }
        var art = draft.Design.Recipe.Artboard ?? throw new ArgumentException("需要整张画板协议。");
        CanvasSurfaceGeometry.ValidateHosting(draft.Design, CanvasLayout.ForAuthoring(draft.Design, art.Width, art.Height));
        session.Workspace?.Verify(draft.Design);
        // Nothing above mutates the actual session or invalidates a valid preview on failure.
        session.Revise(draft.Design);
        var receipt = new CanvasDraftReceipt(request.RequestId, session.DraftRevision, CanvasDeclaration.Digest(draft.Design));
        receipts.Add(request.RequestId, (hash, receipt)); return receipt;
    }
    public void Preview(long revision, string digest)
    { Require(revision, digest); session.Preview(); }
    public void CancelPreview(long revision, string digest)
    { Require(revision, digest); session.Revise(session.Design); }
    private void Require(long revision, string digest)
    { var now = Read(); if (now.DraftRevision != revision || now.DesignDigest != digest) throw new ArgumentException("草稿已变化，预览请求失效。"); }
}
